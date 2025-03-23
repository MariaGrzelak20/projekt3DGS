using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class cameraRead : MonoBehaviour
{


    private String cameraFilename = "images.txt";
    private String cameraFilenameIntr = "cameras.txt";

    /// <summary>
    /// Parametry wewnêtrzne kamery, tutaj potrzebne s¹: id kamery,
    /// Czytane z camera.txt
    /// </summary>
    public struct cameraIntrinsic
    {
        public int cameraID;
        public string model;
        public int width;       //szerokosc obrazu
        public int height;      //wysokosc obrazu
        public float focal_length;
        public int imageCenterX;
        public int imageCenterY;

        public cameraIntrinsic(int cam, string mod, int wid, int heig, float fov, int imX, int imY)
        {
            cameraID = cam;
            model = mod;
            width = wid;
            height = heig;
            focal_length = fov;
            imageCenterX = imX;
            imageCenterY = imY;
        }

    }

    /// <summary>
    /// Parametry takie jak pozycja kamery, id kamery, id obrazy, obserwowanego przez kamere, rotacja kamery, nazwa obrazu obserwowanego przez kamere
    /// czytane z images.txt
    /// 
    /// </summary>
    public struct cameraExtrinsic
    {
        public int cameraID;
        public int imageID;
        public String imageName;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;

        public cameraExtrinsic(int cId, int iId, String imgName, Vector3 pos, Quaternion rot)
        {
            cameraID = cId;
            imageID = iId;
            imageName = imgName;
            cameraPosition = pos;
            cameraRotation = rot;
        }
    }


    /// <summary>
    /// Czytany z images.txt
    /// </summary>
    public List<cameraExtrinsic> readCameraExtrinsics()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, cameraFilename);

        List<cameraExtrinsic> listCameraE = new List<cameraExtrinsic>();

        if (File.Exists(filePath))
        {
            String[] content = File.ReadAllText(filePath).Split("\n");
            bool header = true;
            String temp = "";//zmienna temp do przechowywania
            String[] tempIter;//zmienna temp do uzycia w iteracji
            int nextId = 0;

            for (int i = 0; i < content.Length; i++)
            {
                temp = content[i].Replace('.', ',');

                tempIter = temp.Split(' ');


                if (!header && i == nextId && tempIter.Length == 10)//sprawdzamy czy nie jestesmy w headerze i czy na odpowiedniej linijce - tylko co druga linijka zawiera parametry
                {

                    //Debug.Log(temp.ToString());
                    //Debug.Log(tempIter[0]+" "+ tempIter[1] + " " + tempIter[2] + " " + tempIter[3] + " " + tempIter[4] + " " + tempIter[5] + " " + tempIter[6] + " " + tempIter[7] + " " + tempIter[8] + " " + tempIter[9]);
                    nextId += 2;
                    int h = 0;
                    int.TryParse(tempIter[0], out h);
                    int j = 0;
                    int.TryParse(tempIter[8], out j);
                    Vector3 position = new Vector3(
                            float.Parse(tempIter[5]),
                            float.Parse(tempIter[6]),
                            float.Parse(tempIter[7])
                            );
                    Quaternion rotation = new Quaternion(
                            float.Parse(tempIter[2]),
                            float.Parse(tempIter[3]),
                            float.Parse(tempIter[4]),
                            float.Parse(tempIter[1])
                            );
                    rotation.Set(
                            float.Parse(tempIter[4]),
                            float.Parse(tempIter[1]),
                            float.Parse(tempIter[2]),
                            float.Parse(tempIter[3])
                            );
                    rotation.Normalize(); // bardzo wa¿ne!

                    // 1. Inwersja rotacji COLMAP  z camera-to-world
                    Quaternion unityRot = Quaternion.Inverse(rotation);

                    rotation = rotation * Quaternion.Euler(0, 180, 0);
                    Vector3 cameraPos = -(rotation * position);
                    
                    //Debug.Log("Rotacja: " + rotation.x+" "+rotation.y+" "+rotation.z);
                    tempIter[9] = tempIter[9].Replace(",", ".");
                    cameraExtrinsic cam = new cameraExtrinsic(
                        h,
                        j,
                        tempIter[9],
                        cameraPos,
                        rotation
                        );
                    listCameraE.Add(cam);
                }
                else
                {
                    if (temp.Contains("Number of images"))
                    {
                        header = false;
                        nextId = i + 1;
                    }
                }
            }



        }



        return listCameraE;
    }

    public Vector3 cameraPositionForUnity(Vector3 cPosition,Quaternion cRotation) 
    {

        Quaternion rot = cRotation;
        //rot.y *= -1;
        //rot.z *= -1;
        //C=-R(transponowane)*t
        Vector3 pos = cPosition;
        //pos.y*= -1;
        //pos.z *= -1;
        //Quaternion rot = Quaternion.Inverse(cRotation); // transformacja COLMAP  Unity
        Debug.Log("Dane w funkcji: " + cPosition + " " + cRotation);
        Debug.Log("Dane w funkcji: " + pos + " " + rot);
        // Oblicz pozycjê obiektu (w Unity: position = -R * C)
        Matrix4x4 R = Matrix4x4.Rotate(rot);
        Debug.Log("Matryca:\n "+R.ToString());

        Vector3 position = -(R.transpose.MultiplyVector(pos)); ;

        // Stwórz GameObject jako kamerê

        return position;
    }

    

    public List<cameraExtrinsic> readCameras2()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, cameraFilename);

        List<cameraExtrinsic> listCameraE = new List<cameraExtrinsic>();

        if (File.Exists(filePath))
        {
            
            string[] lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("#") || line == "") continue;

                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 10) continue;

                int imageID = int.Parse(parts[0]);
                int cameraID = int.Parse(parts[0]);
                float qw = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                float qx = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                float qy = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                float qz = float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture);

                float tx = float.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture);
                float ty = float.Parse(parts[6], System.Globalization.CultureInfo.InvariantCulture);
                float tz = float.Parse(parts[7], System.Globalization.CultureInfo.InvariantCulture);

                string imageName = parts[9];

                cameraExtrinsic cam = new cameraExtrinsic
                {
                    imageID = imageID,
                    cameraID = cameraID,
                    imageName = imageName,
                    cameraPosition = new Vector3(tx, ty, tz),
                    cameraRotation = new Quaternion(qx, qy, qz, qw),
                    
                };

                listCameraE.Add(cam);

                //Debug.Log(cam.cameraPosition.ToString()+" "+cam.cameraRotation.ToString()+" "+cam.imageName);

                i++; // skip 2nd line (keypoints)
            }

        }
   


        return listCameraE;
    }
    public List<cameraIntrinsic> readCameraIntrinsic() 
    {
        List<cameraIntrinsic> cameraIntrinsics = new List<cameraIntrinsic>();

        string filePath = Path.Combine(Application.streamingAssetsPath, cameraFilenameIntr);


        if (File.Exists(filePath))
        {
            String[] content = File.ReadAllText(filePath).Split("\n");
            bool header = true;
            String temp = "";//zmienna temp do przechowywania
            String[] tempIter;//zmienna temp do uzycia w iteracji
           

            for (int i = 0; i < content.Length; i++)
            {
                temp = content[i].Replace('.', ',');

                tempIter = temp.Split(' ');


                if (!header && tempIter.Length > 3)//sprawdzamy czy nie jestesmy w headerze i czy na odpowiedniej linijce - tylko co druga linijka zawiera parametry
                {
                    int id = 0;     int.TryParse(tempIter[0], out id);
                    int wid = 0;    int.TryParse(tempIter[2], out wid);
                    int hei = 0;    int.TryParse(tempIter[3], out hei);
                    float fov;      float.TryParse(tempIter[4], out fov);
                    int imX = 0;    int.TryParse(tempIter[5], out imX);
                    int imY = 0;    int.TryParse(tempIter[6], out imY);
                    cameraIntrinsic cam = new cameraIntrinsic(
                        id, tempIter[1],wid,hei,fov,imX,imY
                        );
                    cameraIntrinsics.Add(cam);
                }
                else
                {
                    if (temp.Contains("Number of cameras"))
                    {
                        header = false;
                    }
                }
            }



        }

        return cameraIntrinsics;
    }
}
