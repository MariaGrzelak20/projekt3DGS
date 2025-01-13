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

public class splat : MonoBehaviour
{

   
    public struct splatStruct
    {
        
        public Vector3 position;
        //Covariance Matrix is stored as array of 3 Vector 3 - stored data is not too big
        public Vector3[] covMatrix;
        //potrzebny skrypt na obliczanie hsrmonik sferycznych, na razie wczytywana srednia 
        public UnityEngine.Color sh;
        public float splatOpacity;

        public splatStruct(Vector3 pos, Vector3[] covMatrix, UnityEngine.Color sh,float splatOpacity)
            {
            position = pos;
            this.covMatrix = covMatrix;
            this.sh = sh;
            this.splatOpacity = splatOpacity;
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

    public Vector3[] getCovarianceMatrix(Vector3 splatCenterPosition,Vector3[] points) 
    {

        Vector3[] matrix = new Vector3[3];

        // Inicjalizuj macierz zerami
        for (int i = 0; i < 3; i++)
        {
            matrix[i] = Vector3.zero;
        }

        /*Debug.Log("Poczatkowe dane:" +
            "\nSrednia wartosc:" + splatCenterPosition +
            "\nWartosci punktow: " +
            "\n" + points[0] +
            "\n" + points[1] +
            "\n" + points[2]);
        */
        if (points.Count() > 2)
        {
            // Iteruj przez punkty i sumuj wk³ady do macierzy
            for (int i = 0; i < 3; i++)
            {
                Vector3 diff = points[i] - splatCenterPosition;

                matrix[0] += new Vector3(diff.x * diff.x, diff.x * diff.y, diff.x * diff.z);
                matrix[1] += new Vector3(diff.y * diff.x, diff.y * diff.y, diff.y * diff.z);
                matrix[2] += new Vector3(diff.z * diff.x, diff.z * diff.y, diff.z * diff.z);

               /* Debug.Log("Dla i rownego:" + i +
                    "\nwartosc diff:" + diff +
                    "\n wartosci matrycy:" +
                    "\n" + matrix[0] +
                    "\n" + matrix[1] +
                    "\n" + matrix[2]);*/
            }

            // Uœrednij elementy macierzy po zakoñczeniu sumowania
            for (int i = 0; i < 3; i++)
            {
                matrix[i] /= 3;
            }
        }

        return matrix;
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
