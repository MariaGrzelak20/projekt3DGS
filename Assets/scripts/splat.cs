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

public class splat : MonoBehaviour
{

    //poprawic jakie typy do nich
    struct splatStruct
    {

        Vector3 position;
        //potrzebne wczytane param wewnêtrzne i zewnêtrzne kamery
        float[] covMatrix;
        //potrzebny skrypt na obliczanie hsrmonik sferycznych
        UnityEngine.Color sh;
        float splatOpacity;


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



    /// <summary>
    /// Czyta punkty z pliku .ply i laduje je do listy splatPoint, gdzie ma pozycje i kolor
    /// </summary>
    /// <returns></returns>
    /*
    public List<splatPoint> readPoints()
    {
        List<splatPoint> points = new List<splatPoint>();
        string filePath = Path.Combine(Application.streamingAssetsPath, plyFileName);

        if (File.Exists(filePath))
        {
            string content = File.ReadAllText(filePath);

            string[] splitContent = content.Split('\n', '\r');
            //Debug.Log(splitContent[8]);
            string temp = "";
            string[] tempArr = new string[7];

            foreach (string con in splitContent)
            {
                //w linijce musi byc 7 parametrow - pozycje xyz, kolory rgb, przezroczystosc, jesli linijka nie ma tych danych nie jest wazna - nie jest punktem
                temp = con.Replace('.', ',');
                tempArr = temp.Split(' ');

                //tam gdzie jest odpowiednia liczba parametrow, ladujemy punkty do listy punktow
                if (tempArr.Length == 7)
                {
                    points.Add(new splatPoint(
                        new Vector3(float.Parse(tempArr[0]), float.Parse(tempArr[1]), float.Parse(tempArr[2])),
                        new UnityEngine.Color(float.Parse(tempArr[3]), float.Parse(tempArr[4]), float.Parse(tempArr[5]), float.Parse(tempArr[6]))
                        ));
                }

            }

        }
        else
        {
            Debug.LogError("Nie ma pliku PLY w StreaminAssets.");
        }

        return points;
    }

    */

    void Start()
    {
        points3DRead obiektDoCzytania = gameObject.AddComponent<points3DRead>();
        List<points3DRead.splatPoint> p = obiektDoCzytania.readPoints();
       // Debug.Log("List of points:" + p.Count());

        List<List<points3DRead.splatPoint>> groups = obiektDoCzytania.FindClosestGroups(p);

       // Debug.Log("List of groups of three:"+groups.Count());
      //  foreach (var group in groups)
      //  {
      //      Debug.Log($"Group: {string.Join(", ", group.Select(p => p.position.ToString()))}");
      //  }
    }

    /// <summary>
    /// splaty tworzone przy inicjalizacji
    /// </summary>
    /// <returns></returns>
    public splat createSplat() 
    {
        splat sp = new splat();
        //przypisanie pozycji sredniej
        //przypisanie matrycy jednostkowek
        //przypisanie 

        return sp;
    }

    /// <summary>
    /// poprawianie parametrow splatu przy treningu
    /// </summary>
    /// <param name="spOld"></param>
    /// <returns></returns>
    public splat updateSplat(splat spOld) 
    {
        splat sp = new splat();
        //przypisanie matrycy
        //przypisanie 
        return sp;
    }

    public Vector3 getMeanPosition(Vector3 firstPos,Vector3 secondPos, Vector3 thirdPos) 
    {
        Vector3 meanPosition = new Vector3();

        return meanPosition;
    }

    public UnityEngine.Color getMeanColor() 
    {
        UnityEngine.Color col = new UnityEngine.Color();
        return col;
    }

    /*
    public List<List<splatPoint>> FindClosestGroups(List<splatPoint> points)
    {
        List<List<splatPoint>> groups = new List<List<splatPoint>>();

        foreach (var point in points)
        {
            // Calculate distances from the current point to all other points
            var distances = points
                .Where(p => p.position != point.position) // Exclude the current point
                .Select(p => new { Point = p, Distance = Vector3.Distance(point.position, p.position) })
                .OrderBy(x => x.Distance) // Sort by distance
                .Take(2) // Take the two closest points
                .Select(x => x.Point) // Get the points only
                .ToList();

            // Add the current point and its two closest points as a group
            var group = new List<splatPoint> { point };
            group.AddRange(distances);

            groups.Add(group);
        }

        return groups;
    }*/
}
