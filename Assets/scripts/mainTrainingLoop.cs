using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static points3DRead;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using static cameraRead;
using static splat;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine.Windows.WebCam;
using System;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class mainTrainingLoop : MonoBehaviour
{
    /// <summary>
    /// Read points from file, keep them in struct, to keep points and colors together
    /// Initialize SplatStruct List - get initial splats
    ///  
    ///in loop: assign compute shader for forward pass
    ///pass data into shader
    ///we get forward pass done, we load shader for backwards pass
    ///last: we update points
    ///
    /// ^ this happens in loop
    /// 
    /// afterwards we can attach create mesh to game object, assign shader showing mesh, and see the result
    /// </summary>
    /// 
    private ComputeBuffer m_Buffer;
    private ComputeBuffer cameraBuffer;
    private Texture2D image;
    private ComputeBuffer lossBuffer;  // Bufor na wynik straty
    private ComputeBuffer sizes;

    [SerializeField]
    string imagesPath;
    public ComputeShader computeShader;
    

    void Start()
    {
        List<splat.splatStruct> splatList = new List<splat.splatStruct>();
        points3DRead reader = gameObject.AddComponent<points3DRead>();
        
        List<splatPoint> points = reader.readPoints();
        splat splatFunctions = gameObject.AddComponent<splat>();

        //read camera positions and the image
        List<cameraExtrinsic> cameraValues = gameObject.GetComponent<cameraRead>().readCameraExtrinsics();

        //scaling points by 1000, because most of them fall to 0,00... and float shows this as 0
        float scaleFactor = 100f;

        
        for (int i = 0; i < points.Count; i++) 
        {
            var p = points[i]; // Copy the struct (value type)
            p.position *= scaleFactor; // Modify the position
            points[i] = p; // Assign the modified struct back to the list
        }


        //assigning values to splats and creating  a list of them, here also decleare spherical harmonic coefficients
        foreach (var point in points)
        {
            // Calculate distances from the current point to all other points
            var distances = points
                .Where(p => (p.position - point.position).sqrMagnitude > 1e-6f)
                .Select(p => new { Point = p, Distance = Vector3.Distance(point.position, p.position) })
                .OrderBy(x => x.Distance) // Sort by distance
                .Take(2) // Take the two closest points
                .Select(x => x.Point) // Get the points only
                .ToList();

            // Add the current point and its two closest points as a group
            var group = new List<splatPoint> { point };
            group.AddRange(distances);
            Vector3 meanPosition = ( group[0].position+ group[1].position + group[2].position) / 3;
            Color meanColor = (point.color + group[0].color + group[1].color) / 3;

            Vector3[] pointsForMatrix = {group[0].position , group[1].position , group[2].position };
           
            double[,] covariance = splatFunctions.getCovarianceMatrix(meanPosition,pointsForMatrix);

            // Tworzymy macierz z danych
            Matrix<double> sigmaMatrix = DenseMatrix.OfArray(covariance);

            // Rozk³ad wartoœci w³asnych
            var evd = sigmaMatrix.Evd(symmetricity: Symmetricity.Symmetric);

            // Macierz rotacji (R)
            Matrix<double> R = evd.EigenVectors;

            // Skala (S) - pierwiastki z wartoœci w³asnych
            Vector<double> eigenValues = evd.EigenValues.Real(); // Wartoœci w³asne

            Vector<double> scale = eigenValues.PointwiseSqrt();  // Pierwiastki


            Vector3 scaleVec = new Vector3(
                (float)scale[0],
                (float)scale[1],
                (float)scale[2]
                );

            Quaternion rotationQuat = ParseMatrixToQuaternion(R);

           //1 is for opacity, it's the starting value, that will later be improved, like all of splat's parameters
            splatList.Add(new splat.splatStruct(
                meanPosition,
                scaleVec,
                rotationQuat,
                1,
                new float[] {meanColor.r,0,0,0,0,0,0,0,0 },
                new float[] {meanColor.g,0,0,0,0,0,0,0,0 },
                new float[] {meanColor.b,0,0,0,0,0,0,0,0 }
                ));
        }
        
        //Debug showing of values
        Debug.Log("Liczba splatow:" + splatList.Count());
        int iter = 0;

        //wyswietlanie danych
        foreach (splat.splatStruct spStr in splatList)
        {

           if ((iter%10)==0)//codziesiaty
           {
                Debug.Log("Dane splata: " +
                    "\nPosition: " + spStr.position
                    + "\n S: " + spStr.scale +
                    "\n R: " + spStr.rotation +
                    "\nColorR: " + spStr.shR[0]+" " + spStr.shR[1] +
                    "\nColorG: " + spStr.shG[0]+" " + spStr.shG[1] +
                    "\nColorB: " + spStr.shB[0]+" " + spStr.shB[1] 
                     );
           }
            iter++;
        }

        //loop1 - general number of complete training iterations
        //complete iteration - iteration that went through all of camera pov, then got gradients for all of splats and upgraded them
        //total value of loss - value indicating how different is generated image compared to gorund truth
        float lossTotalValue = 0;
        
        for (int i = 0; i < 1; i++) 
        {   

            for (int j = 0; j < 4;j++)//cameraValues.Count(); j++) 
            {
                Vector3 camPos = cameraValues[j].cameraPosition;
                string imageN = cameraValues[j].imageName    
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Replace("\"", "")
                    .Replace("?", "")
                    .Replace("*", "");
                string path =  Path.Combine(imagesPath,imageN);
                Debug.Log(path);
               
                //read image and load it into texture 2d
                byte[] file = File.ReadAllBytes(path);
                image = new Texture2D(2, 2);
                image.LoadImage(file);

                //prepeare compute bufer- inside data of splats, current camera position and image used for comparison
                //m_Buffer = new ComputeBuffer(splatList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(splat.splatStruct)));

                // Set the data into the buffer
                // m_Buffer.SetData(splatList);
                List<float> flattenedData = new List<float>();
                foreach (var splat in splatList)
                {
                    flattenedData.AddRange(Flatten(splat));
                }

                // Convert flattened data to an array
                float[] flattenedArray = flattenedData.ToArray();

                // Create and set the ComputeBuffer
                //ComputeBuffer splatBuffer = new ComputeBuffer(flattenedArray.Length, sizeof(float));
                //splatBuffer.SetData(flattenedArray);
                m_Buffer = new ComputeBuffer(flattenedArray.Length, sizeof(float));
                m_Buffer.SetData(flattenedArray);

                
                float[] camPosArr = {camPos.x,camPos.y,camPos.z };
                cameraBuffer = new ComputeBuffer (camPosArr.Length, sizeof(float));
                cameraBuffer.SetData(camPosArr);

                // Bufor na wynik straty
                ComputeBuffer lossBuffer = new ComputeBuffer(1, sizeof(float));
                float[] initialLossValue = new float[1] { 0 };
                lossBuffer.SetData(initialLossValue);

                // Pobranie macierzy widoku i projekcji kamery w Unity
                Matrix4x4 viewMatrix = Camera.main.worldToCameraMatrix;   // Macierz widoku
                Matrix4x4 projectionMatrix = Camera.main.projectionMatrix; // Macierz projekcji

                // Po³¹czenie obu macierzy w jedn¹ ViewProjectionMatrix
                Matrix4x4 viewProjectionMatrix = projectionMatrix * viewMatrix;

                //geting the proper size for shader - has to go over every pixel -needs proper size
                // Get image size
                int imageWidth = image.width;
                int imageHeight = image.height;
                int numSplats = splatList.Count; // Number of splats in the scene

                // Define number of threads per workgroup
                int threadsPerGroupX = 8;
                int threadsPerGroupY = 8;

                // Compute number of workgroups needed
                int threadGroupsX = Mathf.CeilToInt(imageWidth / (float)threadsPerGroupX);
                int threadGroupsY = Mathf.CeilToInt(imageHeight / (float)threadsPerGroupY);


                //we get necesseary sizes - number of splats, image params - width and height

                float[] splatN = { splatList.Count(),imageWidth,imageHeight };
                sizes = new ComputeBuffer(splatN.Length, sizeof(float));
                sizes.SetData(splatN);


                //load bufer into compute shader
                int kernelHandle = computeShader.FindKernel("CSMain");
                computeShader.SetBuffer(kernelHandle, "splatBuffer", m_Buffer);
                computeShader.SetBuffer(kernelHandle, "cameraBuffer", cameraBuffer);
                computeShader.SetBuffer(kernelHandle, "lossBuffer", lossBuffer);
                computeShader.SetTexture(kernelHandle, "groundTruthImage", image);
                computeShader.SetBuffer(kernelHandle, "sizes", sizes);
                computeShader.SetMatrix("ViewProjectionMatrix", viewProjectionMatrix);

                // Dispatch the compute shader (example dispatch size)
                computeShader.Dispatch(kernelHandle, threadGroupsX,threadGroupsY, 1);


                float[] lossResults = new float[1];
                lossBuffer.GetData(lossResults);

                // Oblicz ostateczn¹ stratê (np. suma strat wszystkich pikseli)
                float totalLoss = 0f;
                for (int k = 0; k < lossResults.Length; k++)
                {
                    totalLoss += lossResults[i];  // Zbieranie wyników
                }

                Debug.Log("Total Loss: " + totalLoss);

                // Zwolnienie zasobów
                m_Buffer.Release();
                cameraBuffer.Release();
                lossBuffer.Release(); 

                float lossShaderOutput = totalLoss;
                //we need combined loss value for gradients and splat update
                lossTotalValue += lossShaderOutput;
            }

            //for all, ALL parameters - xyz of position, rgb of colors, R and S, we get gradient using the parameter and loss
            //from that we get new, better value and assign it to the splat
            //all of this has to repeat itself like tousand of times
        }

        //change shader from compute to shader showing the result - for example the GaussianRender from that finished gaussian renderer in unity
    }
    public float[] Flatten(splatStruct splat)
    {
        
        List<float> flattenedData = new List<float>();

        // Add position, scale, and rotation to the flattened list
        flattenedData.AddRange(new float[] { splat.position.x, splat.position.y, splat.position.z });
        flattenedData.AddRange(new float[] { splat.scale.x, splat.scale.y, splat.scale.z });
        flattenedData.AddRange(new float[] { splat.rotation.x, splat.rotation.y, splat.rotation.z, splat.rotation.w });

        // Add spherical harmonics coefficients to the flattened list
        flattenedData.AddRange(splat.shR);
        flattenedData.AddRange(splat.shG);
        flattenedData.AddRange(splat.shB);
        flattenedData.AddRange(new float[] { splat.opacity });

        return flattenedData.ToArray();
    }

    Quaternion ParseMatrixToQuaternion(Matrix<double> R)
    {
        float w, x, y, z;

        if (R[0, 0] + R[1, 1] + R[2, 2] > 0)
        {
            float t = (float)(R[0, 0] + R[1, 1] + R[2, 2] + 1);
            float s = 0.5f / Mathf.Sqrt(t);
            w = s * t;
            x = (float)((R[2, 1] - R[1, 2]) * s);
            y = (float)((R[0, 2] - R[2, 0]) * s);
            z = (float)((R[1, 0] - R[0, 1]) * s);
        }
        else if (R[0, 0] > R[1, 1] && R[0, 0] > R[2, 2])
        {
            float t = (float)(1 + R[0, 0] - R[1, 1] - R[2, 2]);
            float s = 0.5f / Mathf.Sqrt(t);
            w = (float)((R[2, 1] - R[1, 2]) * s);
            x = s * t;
            y = (float)((R[0, 1] + R[1, 0]) * s);
            z = (float)((R[0, 2] + R[2, 0]) * s);
        }
        else if (R[1, 1] > R[2, 2])
        {
            float t = (float)(1 - R[0, 0] + R[1, 1] - R[2, 2]);
            float s = 0.5f / Mathf.Sqrt(t);
            w = (float)((R[0, 2] - R[2, 0]) * s);
            x = (float)((R[0, 1] + R[1, 0]) * s);
            y = s * t;
            z = (float)((R[1, 2] + R[2, 1]) * s);
        }
        else
        {
            float t = (float)(1 - R[0, 0] - R[1, 1] + R[2, 2]);
            float s = 0.5f / Mathf.Sqrt(t);
            w = (float)((R[1, 0] - R[0, 1]) * s);
            x = (float)((R[0, 2] + R[2, 0]) * s);
            y = (float)((R[1, 2] + R[2, 1]) * s);
            z = s * t;
        }

        return new Quaternion(x, y, z, w);
    }

}
