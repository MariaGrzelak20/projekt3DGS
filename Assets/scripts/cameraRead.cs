using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class cameraRead : MonoBehaviour
{


    private String cameraFilename = "images.txt";

    /// <summary>
    /// Parametry wewnêtrzne kamery, tutaj potrzebne s¹: id kamery,
    /// Czytane z camera.txt
    /// </summary>
    public struct cameraIntrinsic
    {
        int cameraID;
    }

    /// <summary>
    /// Parametry takie jak pozycja kamery, id kamery, id obrazy, obserwowanego przez kamere, rotacja kamery, nazwa obrazu obserwowanego przez kamere
    /// czytane z images.txt
    /// 
    /// </summary>
    public struct cameraExtrinsic
    {
        int cameraID;
        int imageID;
        String imageName;
        Vector3 cameraPosition;
        Quaternion cameraRotation;

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
                            float.Parse(tempIter[1]),
                            float.Parse(tempIter[2]),
                            float.Parse(tempIter[3]),
                            float.Parse(tempIter[4])
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

    public List<cameraIntrinsic> readCameraIntrinsics() 
    {
        List<cameraIntrinsic> cameraIntrinsics = new List<cameraIntrinsic>();



        return cameraIntrinsics;
    }
   void Start()
    {
       // List<cameraExtrinsic> l = readCameraExtrinsics();
        
       // List<cameraIntrinsic> f = readCameraIntrinsics();
    }
}
