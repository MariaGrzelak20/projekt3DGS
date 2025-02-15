using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;
using System.IO;
using System;
using UnityEngine.UIElements;
using UnityEngine.Rendering.Universal;
using System.Text;
using System.Linq;
using System.Drawing;
using static UnityEditor.PlayerSettings;
using Unity.Mathematics;
using static points3DRead;

public class splat : MonoBehaviour
{

   
    public struct splatStruct
    {
        
        public Vector3 position;
        //Covariance Matrix is stored as array of 3 Vector 3 - stored data is not too big
        //public Vector3[] covMatrix;
        public Vector3 scale;
        public Quaternion rotation;
        public float opacity;
        public float[] shR;
        public float[] shG;
        public float[] shB;


        public splatStruct(Vector3 pos,Vector3 sVec, Quaternion rotQuat,float op, float[] r, float[] g, float[] b)
            {
            position = pos;
            //this.covMatrix = covMatrix;
            scale= sVec;
            rotation = rotQuat;
            opacity = op;
            shR= r;
            shG = g;
            shB= b;
            }

    }

    public struct camera
    {
        public Vector3 position;
        public Vector3 rotation;
        public int id;

        camera(Vector3 pos,Vector3 rot, int id)
        {
            position=pos;
            rotation=rot;
            this.id = id;
        }

    }



    void Start()
    {
        points3DRead obiektDoCzytania = gameObject.AddComponent<points3DRead>();
        List<points3DRead.splatPoint> p = obiektDoCzytania.readPoints();
       

        List<List<points3DRead.splatPoint>> groups = obiektDoCzytania.FindClosestGroups(p);

       // Debug.Log("List of groups of three:"+groups.Count());
      //  foreach (var group in groups)
      //  {
      //      Debug.Log($"Group: {string.Join(", ", group.Select(p => p.position.ToString()))}");
      //  }
    }

    /// poprawianie parametrow splatu przy treningu
    /// </summary>
    /// <param name="spOld"></param>
    /// <returns></returns>

    public double[,] getCovarianceMatrix(Vector3 splatCenterPosition,Vector3[] points) 
    {

        //Vector3[] matrix = new Vector3[3];

        double[,] covariance = new double[3, 3];
        
        if (points.Count() > 2)
        {
            /* // Iteruj przez punkty i sumuj wk³ady do macierzy
             for (int i = 0; i < 3; i++)
             {
                 Vector3 diff = points[i] - splatCenterPosition;

                 matrix[0] += new Vector3(diff.x * diff.x, diff.x * diff.y, diff.x * diff.z);
                 matrix[1] += new Vector3(diff.y * diff.x, diff.y * diff.y, diff.y * diff.z);
                 matrix[2] += new Vector3(diff.z * diff.x, diff.z * diff.y, diff.z * diff.z);



             }

             for (int i = 0; i < 3; i++)
             {
                 matrix[i] /= 3;
             }*/


            foreach (var point in points)
            {
                Vector3 diff = point - splatCenterPosition;

                // Dodaj wk³ad do macierzy kowariancji
                covariance[0, 0] += diff.x * diff.x; // xx
                covariance[0, 1] += diff.x * diff.y; // xy
                covariance[0, 2] += diff.x * diff.z; // xz
                covariance[1, 0] += diff.y * diff.x; // yx
                covariance[1, 1] += diff.y * diff.y; // yy
                covariance[1, 2] += diff.y * diff.z; // yz
                covariance[2, 0] += diff.z * diff.x; // zx
                covariance[2, 1] += diff.z * diff.y; // zy
                covariance[2, 2] += diff.z * diff.z; // zz
            }



            // Uœrednij macierz
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    covariance[i, j] /= points.Length;

            //zapobiega NaN w Scale
            double epsilon = 1e-6;
            for (int i = 0; i < 3; i++)
            {
                covariance[i, i] += epsilon; // Dodanie epsilon do przek¹tnej
            }

        }

        return covariance;
    }

    /// <summary>
    /// Returns list of splats, with declared prameters, requires list of splatPoints, to get positions and colors from them
    /// </summary>
    /// <returns></returns>
    public List<splatStruct> getSplats(List<points3DRead.splatPoint> points) 
    {
        List <splatStruct> splatList = new List<splatStruct>();

        return splatList;
    }
   
}
