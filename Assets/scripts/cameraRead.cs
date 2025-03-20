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

        public cameraIntrinsic(int cam, string mod, int wid, int heig,float fov, int imX, int imY) 
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


                if (!header && i == nextId && tempIter.Length > 5)//sprawdzamy czy nie jestesmy w headerze i czy na odpowiedniej linijce - tylko co druga linijka zawiera parametry
                {


                    nextId += 2;
                    int h = 0;
                    int.TryParse(tempIter[0], out h);
                    int j = 0;
                    int.TryParse(tempIter[8], out j);
                    tempIter[9] = tempIter[9].Replace(",", ".");
                    cameraExtrinsic cam = new cameraExtrinsic(
                        h,
                        j,
                        tempIter[9],
                        new Vector3(
                            float.Parse(tempIter[5]),
                            float.Parse(tempIter[6]),
                            float.Parse(tempIter[7])
                            ),
                        new Quaternion(
                            float.Parse(tempIter[2]),
                            float.Parse(tempIter[3]),
                            float.Parse(tempIter[4]),
                            float.Parse(tempIter[1])
                            )
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
