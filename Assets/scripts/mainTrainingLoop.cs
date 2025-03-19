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
using UnityEngine.UI;
using UnityEngine;
//using Emgu.CV;
//using Emgu.CV.CvEnum;
//using Emgu.CV.Structure;

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
    private Texture2D imageR;               //image Read from file (ground truth images)
    private ComputeBuffer sizes;

    //buffers for loss shader
    private ComputeBuffer lossBufferDssim;  // Bufor na wynik straty
    private ComputeBuffer lossBufferL1;     // Bufor na wynik straty

    [SerializeField]
    string imagesPath;
    public ComputeShader computeShader;
    public ComputeShader dssimShader;
    public ComputeShader l1shader;

    [SerializeField]
    GameObject planeRenderSplat;
    private float totalDssimLoss = 0;
    private float totalL1Loss = 0;


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
           // p.position *= scaleFactor; // Modify the position
            points[i] = p; // Assign the modified struct back to the list
        }


        //assigning values to splats and creating  a list of them, here also decleare spherical harmonic coefficients
        foreach (var point in points)
        {
            
            
            Vector3 meanPosition = point.position;
            Color meanColor = point.color;

                // ZnajdŸ 3 najbli¿sze punkty
                var nearestPoints = points
                    .Where(p => p.position != point.position) // Pomijamy sam punkt
                    .Select(p => new { Point = p, Distance = Vector3.Distance(p.position, point.position) })
                    .OrderBy(p => p.Distance)
                    .Take(3)
                    .Select(p => p.Point)
                    .ToList();

                // Oblicz œredni¹ odleg³oœæ do tych punktów
                float meanDist = nearestPoints.Average(p => Vector3.Distance(p.position, point.position));

                // Skala splata jako œrednia odleg³oœæ do 3 punktów
                Vector3 scale = new Vector3(meanDist, meanDist, meanDist);
            //Debug.Log("Skala: " + meanDist);
            /*
                Vector3[] pointsForMatrix = { point.position };

            double[,] covariance = splatFunctions.getCovarianceMatrix(meanPosition, pointsForMatrix);

            // Tworzymy macierz z danych
            Matrix<double> sigmaMatrix = DenseMatrix.OfArray(covariance);

            // Rozk³ad wartoœci w³asnych
            var evd = sigmaMatrix.Evd(symmetricity: Symmetricity.Symmetric);

            // Macierz rotacji (R)
            Matrix<double> R = evd.EigenVectors;

            // Skala (S) - pierwiastki z wartoœci w³asnych
            Vector<double> eigenValues = evd.EigenValues.Real(); // Wartoœci w³asne

            //Vector<double> scale = eigenValues.PointwiseSqrt();  // Pierwiastki
            
            //Debug.Log("EignVal: " + eigenValues);
            */


            //scale Vector
            //Vector3 scaleVec = new Vector3(1f,1f,1f);

            //Quaternion rotationQuat = ParseMatrixToQuaternion(R);
            Quaternion rotationQuat = new Quaternion(1,0,0,0);

            

           //1 is for opacity, it's the starting value, that will later be improved, like all of splat's parameters
            splatList.Add(new splat.splatStruct(
                meanPosition,
                scale,
                rotationQuat,
                1,
                new float[] { meanColor.r, 0.3f, 0.3f, 0.3f, 0, 0, 0, 0, 0 },
                new float[] { meanColor.g, 0.3f, 0.3f, 0.3f, 0, 0, 0, 0, 0 },
                new float[] { meanColor.b, 0.3f, 0.3f, 0.3f, 0, 0, 0, 0, 0 }
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
                Debug.Log("Dane splata: nr splata:"+(iter+1) +
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

            for (int j = 10; j < 11;j++)//cameraValues.Count(); j++) 
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
                imageR = new Texture2D(2, 2);
                imageR.LoadImage(file);

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
                
                

                m_Buffer = new ComputeBuffer(flattenedArray.Length, sizeof(float));

                m_Buffer.SetData(flattenedArray);

                
                float[] camPosArr = {camPos.x,camPos.y,camPos.z };
                cameraBuffer = new ComputeBuffer (camPosArr.Length, sizeof(float));
                cameraBuffer.SetData(camPosArr);

                

                // Pobranie macierzy widoku i projekcji kamery w Unity
                Matrix4x4 viewMatrix = Camera.main.worldToCameraMatrix;   // Macierz widoku
                Matrix4x4 projectionMatrix = Camera.main.projectionMatrix; // Macierz projekcji

                // Po³¹czenie obu macierzy w jedn¹ ViewProjectionMatrix
                Matrix4x4 viewProjectionMatrix = projectionMatrix * viewMatrix;

                //geting the proper size for shader - has to go over every pixel -needs proper size
                // Get image size
                int imageWidth = imageR.width;
                int imageHeight = imageR.height;
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
                
                

                //load bufer into compute shader - for now it just generates image from splats
                int kernelHandle = computeShader.FindKernel("CSMain");
                computeShader.SetBuffer(kernelHandle, "splatBuffer", m_Buffer);
                computeShader.SetBuffer(kernelHandle, "cameraBuffer", cameraBuffer);
                computeShader.SetTexture(kernelHandle, "groundTruthImage", imageR);
                computeShader.SetBuffer(kernelHandle, "sizes", sizes);
                computeShader.SetMatrix("ViewProjectionMatrix", viewProjectionMatrix);


                RenderTexture outputTexture;
                outputTexture = new RenderTexture(imageWidth, imageHeight, 0, RenderTextureFormat.ARGBFloat);
                outputTexture.enableRandomWrite = true;
                outputTexture.Create();

                // Przekazanie tekstury do Compute Shader
                computeShader.SetTexture(kernelHandle, "Result", outputTexture);
                
                // Dispatch the compute shader (example dispatch size)
                computeShader.Dispatch(kernelHandle, threadGroupsX,threadGroupsY, 1);
                

                RenderTexture.active = outputTexture;
                Texture2D debugTexture = new Texture2D(outputTexture.width, outputTexture.height, TextureFormat.RGBAFloat, false);
                debugTexture.ReadPixels(new Rect(0, 0, outputTexture.width, outputTexture.height), 0, 0);
                debugTexture.Apply();

                // Przypisanie wyniku do materia³u w Unity
                MeshRenderer meshRenderer = planeRenderSplat.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    if (meshRenderer.material == null)
                    {
                        Debug.LogWarning("Brak materia³u, tworzê nowy.");
                        meshRenderer.material = new Material(Shader.Find("Standard"));
                    }
                    meshRenderer.material.SetTexture("_MainTex", outputTexture);
                }
                RawImage image = FindObjectOfType<RawImage>(); // Znajduje pierwszy `RawImage` w scenie

                if (image != null)
                {
                  
                    image.texture = debugTexture;
                    RectTransform rt = image.GetComponent<RectTransform>();

                    // Ustawienie rozmiaru RawImage na rozmiar tekstury
                    rt.sizeDelta = new Vector2(debugTexture.width, debugTexture.height);
                }
                else
                {
                    Debug.LogError("Nie znaleziono RawImage w scenie!");
                }
                CheckTextureContents(outputTexture,imageR);

                byte[] bytes = debugTexture.EncodeToPNG(); // Mo¿esz u¿yæ EncodeToJPG()
                System.IO.File.WriteAllBytes(Application.dataPath + "/" + "fileName.png", bytes);
                Debug.Log("Zapisano teksturê jako: " + Application.dataPath + "/" + "fileName");


                // Zwolnienie zasobów
                m_Buffer.Release();
                cameraBuffer.Release();

                // Bufor na wynik straty
                int bufferSize = imageWidth * imageHeight; // Jeden float na piksel
                ComputeBuffer lossBuffer = new ComputeBuffer(bufferSize, sizeof(float));
                float[] initialLossValue = new float[1] { 0 };
                lossBuffer.SetData(initialLossValue);

                
                //buffer for dssim
                int kernelHandleDssim = dssimShader.FindKernel("Dssim");
                dssimShader.SetTexture(kernelHandleDssim, "groundTruthImage", imageR);
                dssimShader.SetTexture(kernelHandleDssim, "renderedImage", outputTexture);
                dssimShader.SetBuffer(kernelHandleDssim, "sizes", sizes);
                dssimShader.SetBuffer(kernelHandleDssim, "lossBuffer", lossBuffer);
                dssimShader.Dispatch(0, imageWidth / 8, imageHeight / 8, 1);
                float[] lossData= new float[bufferSize];
                // Pobranie danych z ComputeBuffer do CPU
                lossBuffer.GetData(lossData);

                // Przetwarzanie wartoœci - obliczenie œredniego L1 Loss
                float totalLoss = 0;
                foreach (var val in lossData)
                {
                    totalLoss += val;
                }
                float meanDssimLoss = totalLoss ;
                totalDssimLoss += meanDssimLoss;

                Debug.Log($" Œredni Dssim Loss: {meanDssimLoss}");
               
                //zwolnienie zasobow
                lossBuffer.Release();


                // Bufor na wynik straty
                int bufferSize2 = imageWidth * imageHeight; // Jeden float na piksel
                ComputeBuffer lossBuffer2 = new ComputeBuffer(bufferSize2, sizeof(float));
                float[] initialLossValue2 = new float[1] { 0 };
                lossBuffer2.SetData(initialLossValue2);
                //buffer for l1
                int kernelHandleL1loss = l1shader.FindKernel("ComputeL1Loss");
                l1shader.SetTexture(kernelHandleL1loss, "groundTruthImage", imageR);
                l1shader.SetTexture(kernelHandleL1loss, "renderedImage", outputTexture);
                l1shader.SetBuffer(kernelHandleL1loss, "sizes", sizes);
                l1shader.SetBuffer(kernelHandleL1loss, "lossBuffer", lossBuffer2);
                l1shader.Dispatch(0, imageWidth / 8, imageHeight / 8, 1);
                float[] lossData2 = new float[bufferSize2];
                // Pobranie danych z ComputeBuffer do CPU
                lossBuffer2.GetData(lossData2);

                // Przetwarzanie wartoœci - obliczenie œredniego L1 Loss
                float totalLoss2 = 0;
                foreach (var val in lossData2)
                {
                    totalLoss2 += val;
                    
                }
                float meanL1Loss = totalLoss2 ;
                totalL1Loss += meanL1Loss;

               // Debug.Log($" Œredni L1 Loss: {meanL1Loss}");
               
                //zwolnienie zasobow
                //zwolnienie zasobow
                lossBuffer2.Release();
                
                sizes.Release();

                //Debug.Log("Pozycja kamery"+camPos.ToString());

                //sum the loss?

               // Mat img1 = TextureToMat(imageR);
               // Mat img2 = TextureToMat(debugTexture);

                // Oblicz DSSIM i L1 Loss
                //double dssim = ComputeDSSIM(img1, img2);
                //double l1Loss = ComputeL1Loss(img1, img2);

                //Debug.Log($"DSSIM: {dssim}");
                //Debug.Log($"L1 Loss: {l1Loss}");


            }

            Debug.Log("Osttateczne bledy: " + totalDssimLoss + " " + totalL1Loss);

            //for all, ALL parameters - xyz of position, rgb of colors, R and S, we get gradient using the parameter and loss
            //from that we get new, better value and assign it to the splat
            //all of this has to repeat itself like tousand of times
        }

        //change shader from compute to shader showing the result - for example the GaussianRender from that finished gaussian renderer in unity
    }
    /*
    Mat TextureToMat(Texture2D texture)
    {
        byte[] imageData = texture.EncodeToPNG();
        Mat mat = new Mat();
        CvInvoke.Imdecode(new Emgu.CV.Util.VectorOfByte(imageData), ImreadModes.Grayscale, mat);
        return mat;
    }

    double ComputeDSSIM(Mat img1, Mat img2)
    {
        // Konwersja do 32-bitowej precyzji
        Mat img1Float = new Mat();
        Mat img2Float = new Mat();
        img1.ConvertTo(img1Float, DepthType.Cv32F);
        img2.ConvertTo(img2Float, DepthType.Cv32F);

        // Obliczenie SSIM przez mno¿enie pikseli
        Mat product = new Mat();
        CvInvoke.Multiply(img1Float, img2Float, product);

        MCvScalar meanSSIM = CvInvoke.Mean(product);
        double ssim = meanSSIM.V0;  // Pobranie wartoœci

        return (1.0 - ssim) / 2.0;  // DSSIM = (1 - SSIM) / 2
    }
   
    double ComputeL1Loss(Mat img1, Mat img2)
    {
        Mat absDiff = new Mat();
        CvInvoke.AbsDiff(img1, img2, absDiff);
        MCvScalar l1Loss = CvInvoke.Mean(absDiff);
        return l1Loss.V0;
    }
 */
    void CheckTextureContents(RenderTexture renderTexture, Texture2D groundTruthImage)
    {
        RenderTexture.active = renderTexture;
        Texture2D tempTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false);
        tempTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        tempTexture.Apply();

        Color firstPixel = tempTexture.GetPixel(0, 0);
        Color centerPixel = tempTexture.GetPixel(renderTexture.width / 2, renderTexture.height / 2);
        Color lastPixel = tempTexture.GetPixel(renderTexture.width - 1, renderTexture.height - 1);

        // Debug.Log($"Pixel (0,0): {firstPixel}");
        // Debug.Log($"Pixel (center): {centerPixel}");
        // Debug.Log($"Pixel (last): {lastPixel}");
        Debug.Log("Ilosc pikseli:" + renderTexture.width * renderTexture.height);
        int num = 0;
        Color sprawdzajacy = new Color(0, 0, 0);
        for (int i = 0; i < tempTexture.width; i++)
        {
            for (int j = 0; j < tempTexture.height; j++)
            {
                if (tempTexture.GetPixel(i, j) == groundTruthImage.GetPixel(i, j))
                {
                    Debug.Log("Pixel color nr:" + i + "  "+j+" " + tempTexture.GetPixel(i, j));
                    num++;

                    // Odczytanie wartoœci z obu obrazów
                    Color gtPixel = groundTruthImage.GetPixel(i, j);
                    Debug.Log($"Ground Truth Pixel ({i},{j}): {gtPixel}");
                }
            }
        }
        Debug.Log("Ilosc  zakolorowanych pikseli:" + num);
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

    Quaternion ParseMatrixToQuaternion(MathNet.Numerics.LinearAlgebra.Matrix<double> R)
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


    //public static double V3Distance(Vector3[] p1, Vector3[] p2)
    //{
        //return Math.Sqrt(
           // Math.Pow(p1[0] - p2[0], 2) +
           // Math.Pow(p1[1] - p2[1], 2) +
           // Math.Pow(p1[2] - p2[2], 2));
   // }
}
