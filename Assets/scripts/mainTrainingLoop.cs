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
        //Czytanie splatow ASCII
        List<splat.splatStruct> splatList = new List<splat.splatStruct>();
        points3DRead reader = gameObject.AddComponent<points3DRead>();
        List<splatPoint> points = reader.readPointBin();


        //Czytanie parametrow kamer
        List<cameraExtrinsic> cameraValues = gameObject.GetComponent<cameraRead>().readCameraExtrinsics();
        List<cameraIntrinsic> cameraIntr = gameObject.GetComponent<cameraRead>().readCameraIntrinsic();
       
        //Inicjalizacja splatow
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

             // Skala splata jako œrednia odleg³oœæ do 3 punktów, inicjalizacja jako izotropowy splat
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


            //Brak rotacji
            Quaternion rotationQuat = new Quaternion(1,0,0,0);

           //Inicjalizacja splatow izotropowych na poczatek
            splatList.Add(new splat.splatStruct(
                meanPosition,
                scale*100,
                rotationQuat,
                1,
                new float[] { meanColor.r, 0.3f, 0.3f, 0.3f, 0, 0, 0, 0, 0 },
                new float[] { meanColor.g, 0.3f, 0.3f, 0.3f, 0, 0, 0, 0, 0 },
                new float[] { meanColor.b, 0.3f, 0.3f, 0.3f, 0, 0, 0, 0, 0 }
                ));

           
        }

        showSplatData(splatList);
        
        
        //Petle treningowe
        for (int i = 0; i < 1; i++) 
        {
            //Wartosc straty, dla wszystkich widokow
            float lossTotalValue = 0;


            //Petle zliczania wartosci bledu dla wszystkich widokow (kamer)
            for (int j = 3; j < 4;j++)//cameraValues.Count(); j++)    //ograniczneie do jednego obrazu
            {
                Vector3 camPos = cameraValues[j].cameraPosition;    //pozycja kamery
                string imageN = cameraValues[j].imageName           //nazwa powiazanego obrazu treningowego
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Replace("\"", "")
                    .Replace("?", "")
                    .Replace("*", "");
                string path =  Path.Combine(imagesPath,imageN);
                Debug.Log(path);
               Debug.Log("Pozycja i obrot kamery nr " + cameraValues[j].cameraID+" " + cameraIntr[j].cameraID +": \n" + cameraValues[j].cameraPosition.ToString() + "\n" + cameraValues[j].cameraRotation.ToString());

                //Wczytanie obrazu treningowego
                byte[] file = File.ReadAllBytes(path);
                imageR = new Texture2D(2, 2);
                imageR.LoadImage(file);

                /*
                //Splaszczenie danych do uzycia w compute shader
                List<float> flattenedData = new List<float>();
                foreach (var splat in splatList)
                {
                    flattenedData.AddRange(Flatten(splat));
                }

                //Zmiana danych z listy na array
                float[] flattenedArray = flattenedData.ToArray();
                
                //utworzenie buffera z danymi splatow
                m_Buffer = new ComputeBuffer(flattenedArray.Length, sizeof(float));
                m_Buffer.SetData(flattenedArray);

                //utworzenie buffera z danymi kamer
                float[] camPosArr = {camPos.x,camPos.y,camPos.z };
                cameraBuffer = new ComputeBuffer (camPosArr.Length, sizeof(float));
                cameraBuffer.SetData(camPosArr);

                // Pobranie macierzy widoku i projekcji kamery w Unity
                Matrix4x4 viewMatrix = Camera.main.worldToCameraMatrix;     // Macierz widoku
                Matrix4x4 projectionMatrix = Camera.main.projectionMatrix;  // Macierz projekcji

                // Po³¹czenie obu macierzy w jedn¹ ViewProjectionMatrix
                Matrix4x4 viewProjectionMatrix = projectionMatrix * viewMatrix;

                //Pobranie rozmiarow obrazu
                int imageWidth  = imageR.width;
                int imageHeight = imageR.height;
                int numSplats   = splatList.Count; // Number of splats in the scene

                // Define number of threads per workgroup
                int threadsPerGroupX = 8;
                int threadsPerGroupY = 8;

                // Compute number of workgroups needed
                int threadGroupsX = Mathf.CeilToInt(imageWidth / (float)threadsPerGroupX);
                int threadGroupsY = Mathf.CeilToInt(imageHeight / (float)threadsPerGroupY);


                //Wczytanie rozmiarow obrazu do bufora
                float[] splatN = { splatList.Count(),imageWidth,imageHeight };
                sizes = new ComputeBuffer(splatN.Length, sizeof(float));
                sizes.SetData(splatN);

                //Przypisanie buforow do compute shadera CSMain
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
                //CheckTextureContents(outputTexture,imageR);

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

               
                lossBuffer2.Release();
                
                sizes.Release();

                */
                Texture2D tex = renderSplatImage(cameraValues[j], cameraIntr[j], splatList);
            }

           
        }

        //change shader from compute to shader showing the result - for example the GaussianRender from that finished gaussian renderer in unity
    }
    
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
                Debug.Log("Pixel color nr:" + i + "  "+j+" " + tempTexture.GetPixel(i, j));
               /* if (tempTexture.GetPixel(i, j) == groundTruthImage.GetPixel(i, j))
                {
                   
                    num++;

                    // Odczytanie wartoœci z obu obrazów
                    Color gtPixel = groundTruthImage.GetPixel(i, j);
                    Debug.Log($"Ground Truth Pixel ({i},{j}): {gtPixel}");
                }*/
            }
        }
        Debug.Log("Ilosc  zakolorowanych pikseli:" + num);
    }


    public void showSplatData(List<splat.splatStruct> splatList) 
    {
        int iter = 0;
        //Debug showing of values
        Debug.Log("Liczba splatow:" + splatList.Count());
        //wyswietlanie danych

        foreach (splat.splatStruct spStr in splatList)
        {

            if (iter < 10)
            {
                Debug.Log("Dane splata: nr splata:" + (iter + 1) +
                    "\nPosition: " + spStr.position
                    + "\n S: " + spStr.scale +
                    "\n R: " + spStr.rotation +
                    "\nColorR: " + spStr.shR[0] + " " + spStr.shR[1] +
                    "\nColorG: " + spStr.shG[0] + " " + spStr.shG[1] +
                    "\nColorB: " + spStr.shB[0] + " " + spStr.shB[1]
                     );
            iter++;
            }
            else { break; }
            
        }
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

    //klatka przechowuje liste indeksow splatow ktore na nia nachodz¹, wraz z ich g³êbokoœciami w view frustrum
    public struct tile 
    {
        
        public List<int> splatWskaznik;
        public List<float> splatOdleglosc;

        //Przechowywanie dla szybszego sortowania, na jakich pozycjach jest dana krawedz
        public int idTile;
        public float xLeft;
        public float yBottom;

        public tile(List<int>wsk, List<float> odl,int id,int x,int y) 
        {
            splatWskaznik = wsk;
            splatOdleglosc = odl;
            idTile = id;
            xLeft = x;
            yBottom = y;
        }

        public void tileAdd(int splatWsk, float splatOdl, float x, float y) 
        {
            this.splatWskaznik.Add(splatWsk);
            this.splatOdleglosc.Add(splatOdl);
            this.xLeft = x;
            this.yBottom = y;
        }
    }

    public Texture2D renderSplatImage(cameraExtrinsic camEx, cameraIntrinsic camIntr, List<splat.splatStruct> splatList) 
    {
        Texture2D outputImage = new Texture2D(2,2);

        //sprawdzenie czy splat jest w view frustrum i klatce

        //1.ustalenie kamery zgodnie z parametrami z COLMAP

        //Quaternion r = this.gameObject.transform.rotation;
        //Quaternion rotat = new Quaternion(r.x, r.y , r.z + 180, r.w);
        //this.gameObject.transform.rotation = rotat;

        Camera.main.transform.position  =   camEx.cameraPosition;
        Quaternion rot = new Quaternion(camEx.cameraRotation.x, camEx.cameraRotation.y, camEx.cameraRotation.z,camEx.cameraRotation.w);
        Camera.main.transform.rotation  =   camEx.cameraRotation;
        /*
        // Translacja kamery COLMAP
        Vector3 translation = camEx.cameraPosition;
        Quaternion rotation = camEx.cameraRotation;

        // Odwracamy translacjê (COLMAP u¿ywa -Z jako przód)
        Vector3 cameraPosition = -(rotation * translation);

        // Obracamy kamerê o 180° wokó³ X, aby patrzy³a w stronê +Z
        Quaternion fixRotation = Quaternion.Euler(180, 0, 0);
        Quaternion cameraRotation = rotation * fixRotation;

        // Ustawienie kamery w Unity
        Camera.main.transform.position = cameraPosition;
        Camera.main.transform.rotation = cameraRotation;
        */
        //Camera.main.fieldOfView = ComputeFOV(camIntr.focal_length, camIntr.width);
        //Camera.main.fieldOfView = 60f;

        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);    //pobranie view frustrum

        List<int> splatInViewFrustrum = new List<int>();
        List<splat.splatStruct> splatyDoPokazania = new List<splat.splatStruct>();

        int it = 0;
        foreach (splat.splatStruct s in splatList) 
        {
            float avgRadius = Mathf.Max(s.position.x, s.position.y,s.position.z)*0.5f;
            if (IsSplatInFrustum(s.position,avgRadius)) 
            {
                splatInViewFrustrum.Add(it);

                splatyDoPokazania.Add(s);
            }
            it++;
        }

        showGizmosForSplats(splatyDoPokazania);
        showSplatMesh(splatList);

        Debug.Log("Splats in view frustrum: " + splatInViewFrustrum.Count);

        //lista klatek, i przypisanie im wartosci poczatkowych 
        List<tile> tiles = new List<tile>();

        

        //sprawdzamy z iloma klatkami dany splat sie naklada
        foreach (int iter in splatInViewFrustrum)
        {
            splat.splatStruct temp = splatList[iter];
            Vector3 screenPos = Camera.main.WorldToViewportPoint(temp.position);

            Matrix4x4 R = Matrix4x4.Rotate(temp.rotation);

            // 2. Macierz skalowania (wariancje to kwadraty skali)
            Matrix4x4 S = Matrix4x4.zero;
            S.m00 = temp.scale.x ;
            S.m11 = temp.scale.y ;
            S.m22 = temp.scale.z ;

            Matrix4x4 cov = R * S * S.transpose * R.transpose;

            Matrix4x4 J = Matrix4x4.zero;
            J.m00 = 1.0f / temp.position.z;
            J.m02 = -temp.position.x / (temp.position.z * temp.position.z);
            J.m11 = 1.0f / temp.position.z;
            J.m12 = -temp.position.y / (temp.position.z * temp.position.z);

            Matrix4x4 w = Camera.main.worldToCameraMatrix ;

            Matrix4x4 covScreen = J * w * cov * w.transpose * J.transpose;
            /*Debug.Log(
                "Numer splata: "+iter+"\n"+
                "Pozycja splata: "+temp.position.ToString()+"\n"+
                "Skala splata: "+temp.scale.ToString()+"\n"+
                "Pozycja splata na ekranie: "+screenPos.ToString()+"\n"+
                "Matryca kowariancji: \n"+cov.ToString()+"\n"+
                "Covariance matrix on screen\n"+covScreen.ToString());
            */
            Matrix4x4 sigma2D = new Matrix4x4();
            sigma2D.SetRow(0, covScreen.GetRow(0));
            sigma2D.SetRow(1, covScreen.GetRow(1));

            // Obliczamy eigenvalues (rozmiary Bounding Boxa)
            Vector2 scale2D = ComputeEigenvalues(sigma2D);

            Debug.Log("PArametry dla splatow w view frustrum:\n" +
                "Pozycja na ekranie: " + screenPos.ToString() + "\n" +
                "Eigenvalues: " + scale2D.x + " " + scale2D.y+"\n"+
                "Pozycja w 3d:"+temp.position.ToString());

            // Przeliczenie Bounding Boxa do pikseli ekranu
            float pixelSizeX = scale2D.x * camIntr.width;
            float pixelSizeY = scale2D.y * camIntr.height;

            Rect boundingBox = new Rect(
                screenPos.x * camIntr.width - pixelSizeX * 0.5f,
                screenPos.y * camIntr.height - pixelSizeY * 0.5f,
                pixelSizeX,
                pixelSizeY
            );

            int tileID = 0;
            int tileSizeX = 16;
            int tileSizeY = 16;
            int borderTileSizeX = camIntr.width%16;
            int borderTileSizeY = camIntr.height%16;

            for (int i = 0; i < camIntr.width; i+=16) 
            {
                for (int j = 0; j < camIntr.height; j+=16) 
                {
                    int tileSpaceX = tileSizeX;
                    int tileSpaceY = tileSizeY;
                    if (i + tileSizeX > camIntr.width) { tileSpaceX = borderTileSizeX; }
                    if (j + tileSizeY > camIntr.height) { tileSpaceY = borderTileSizeY; }

                    Rect tile = new Rect(i, j, tileSpaceX, tileSpaceY);

                    if (tile.Overlaps(boundingBox)) 
                    {
                        bool tileExists = false;

                        //sprawdzamy czy juz mamy tile o podanym id, jak nie ma, to dodajemy
                        foreach (tile t in tiles) 
                        {
                            if (t.idTile == tileID) 
                            {
                                Vector3 splatWorldPos = temp.position; // pozycja Gaussianu w World Space
                                Matrix4x4 viewMatrix = Camera.main.worldToCameraMatrix;

                                // Przekszta³camy pozycjê do przestrzeni kamery (View Space)
                                Vector3 splatViewPos = viewMatrix.MultiplyPoint3x4(splatWorldPos);

                                // G³êbokoœæ to wartoœæ Z w View Space (czyli odleg³oœæ od kamery)
                                float depth = splatViewPos.z;
                                t.tileAdd(iter, depth, i, j);
                                tileExists = true;
                            }
                        }

                        if (!tileExists) 
                        {
                           
                        }
                    }
                    


                    tileID++;
                }
            }

        }

        //odrzucenie splatow poza view frustrum

        //przejscie po klatkach w ekranie, wyznaczenie gdzie ktory splat idzie
        

        return outputImage;
    }

    public Material customMaterial;

    public void showSplatMesh(List<splat.splatStruct> lista) 
    {
        Mesh mesh;
        
        customMaterial.SetVector("_CameraPos", Camera.main.transform.position);
        points3DRead reader = gameObject.AddComponent<points3DRead>();
        createMesh meshCreator = gameObject.AddComponent<createMesh>();
        mesh = new Mesh();

        //Loading points and their colors from file
        //List<points3DRead.splatPoint> pointList = new List<points3DRead.splatPoint>();
        //pointList = reader.readPointBin();

        mesh = reader.meshFromSplats(lista);

        int[] indices = Enumerable.Range(0, mesh.vertices.Length).ToArray();
        mesh.SetIndices(indices, MeshTopology.Points, 0);

        // Recalculate bounds
        mesh.RecalculateBounds();
        //Debug.Log($"Mesh bounds: {mesh.bounds}");


        // Assign mesh to the MeshFilter
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;


        // Assign material to the MeshRenderer
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = customMaterial;

        /* foreach (Vector3 a in GetComponent<MeshFilter>().mesh.vertices)
         {
             Debug.Log(a);
         }
        */

        
    }

    public void showGizmosForSplats(List<splat.splatStruct> lista)
    {
        List<Vector3> points;
        points = new List<Vector3>();

        //Loading only the cooridnates of points
        foreach (splat.splatStruct spt in lista)
        {
            points.Add(spt.position);
        }
    }

    Rect GetSplatBoundingBox2D(Vector3 position, Matrix4x4 covarianceMatrix, Camera cam, int screenWidth, int screenHeight)
    {
        // Transformacja pozycji splata do przestrzeni ekranu
        Vector3 screenPos = cam.WorldToViewportPoint(position);

        if (screenPos.z < 0)
            return new Rect(0, 0, 0, 0); // Splat jest za kamer¹

        // Macierz widoku i Jacobian
        Matrix4x4 viewMatrix = cam.worldToCameraMatrix;
        Matrix4x4 jacobian = Matrix4x4.identity; // Uproszczona wersja

        // Przekszta³cenie macierzy kowariancji do przestrzeni kamery
        Matrix4x4 sigmaPrime = jacobian * viewMatrix * covarianceMatrix * viewMatrix.transpose * jacobian.transpose;

        // Usuwamy trzeci wiersz i kolumnê (z)
        Matrix4x4 sigma2D = new Matrix4x4();
        sigma2D.SetRow(0, sigmaPrime.GetRow(0));
        sigma2D.SetRow(1, sigmaPrime.GetRow(1));

        // Obliczamy eigenvalues (rozmiary Bounding Boxa)
        Vector2 scale2D = ComputeEigenvalues(sigma2D);

        // Przeliczenie Bounding Boxa do pikseli ekranu
        float pixelSizeX = scale2D.x * screenWidth;
        float pixelSizeY = scale2D.y * screenHeight;

        return new Rect(
            screenPos.x * screenWidth - pixelSizeX * 0.5f,
            screenPos.y * screenHeight - pixelSizeY * 0.5f,
            pixelSizeX,
            pixelSizeY
        );
    }

    Vector2 ComputeEigenvalues(Matrix4x4 sigma2D)
    {
        float a = sigma2D.m00;
        float b = sigma2D.m01;
        float c = sigma2D.m10;
        float d = sigma2D.m11;

        float trace = a + d;
        float determinant = a * d - b * c;
        float lambda1 = (trace + Mathf.Sqrt(trace * trace - 4 * determinant)) / 2;
        float lambda2 = (trace - Mathf.Sqrt(trace * trace - 4 * determinant)) / 2;

        return new Vector2(Mathf.Sqrt(lambda1), Mathf.Sqrt(lambda2));
    }

    bool IsSplatInFrustum(Vector3 splatPos,float meanRadius)
    {
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

        // Obliczamy promieñ bounding sphere

        foreach (var plane in frustumPlanes)
        {
            if (plane.GetDistanceToPoint(splatPos) < -meanRadius)
            {
                return false; // Obiekt poza frustum
            }
        }

        return true; // Widoczny!
    }
    float ComputeFOV(float focalLength, float imageWidth)
    {
        return 2f * Mathf.Atan(imageWidth / (2f * focalLength)) * Mathf.Rad2Deg;
    }
}
