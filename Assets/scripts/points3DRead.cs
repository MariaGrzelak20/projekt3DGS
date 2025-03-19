using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static splat;

public class points3DRead : MonoBehaviour
{
    // Start is called before the first frame update

    [SerializeField]
    private String imageFolder = "testFolder";
    [SerializeField]
    private String plyFileName = "model.ply";
    [SerializeField]
    private int pointNumberLimit=0;



    public struct splatPoint
    {
        public Vector3 position;
        public UnityEngine.Color color;

        public splatPoint(Vector3 pos, UnityEngine.Color col)
        {
            position = pos;
            color = col;
        }

    }
    /// <summary>
    /// Czyta punkty z pliku .ply i laduje je do listy splatPoint, gdzie ma pozycje i kolor
    /// </summary>
    /// <returns></returns>
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

            if (pointNumberLimit == 0||pointNumberLimit>splitContent.Count()) { pointNumberLimit = 5000; }
            Debug.Log(pointNumberLimit);
            int iterationNum = 0;
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
                    iterationNum++;
                }

                if (iterationNum == pointNumberLimit) break;

            }

        }
        else
        {
            Debug.LogError("Nie ma pliku PLY w StreaminAssets.");
        }

        return points;
    }



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
                .Take(3) // Take the two closest points
                .Select(x => x.Point) // Get the points only
                .ToList();

            // Add the current point and its two closest points as a group
            var group = new List<splatPoint> { };// { point };
            group.AddRange(distances);

            groups.Add(group);
        }

        return groups;
    }


    /// <summary>
    /// Create Mesh from points - get their mean position and mean color, return the mesh 
    /// </summary>
    /// <param name="groupOfThree"></param>
    /// <returns></returns>
    public Mesh meshFromPly(List<List<splatPoint>> groupOfThree) 
    {
        Mesh mesh = new Mesh();
        List<Vector3> meanPosition = new List<Vector3>();
        List<Color> meanColor = new List<Color>();

        foreach (List<splatPoint> splatPoints in groupOfThree) 
        {
            Vector3 meanPos = (splatPoints[0].position+ splatPoints[1].position+ splatPoints[2].position)/3;
            meanPosition.Add(meanPos);


            Color meanCol = (splatPoints[0].color + splatPoints[1].color + splatPoints[2].color) / 3;
            
            meanCol.r /= 255;
            meanCol.g /= 255;
            meanCol.b /= 255;
            

            meanColor.Add(meanCol);
            //Debug.Log("Kolor:"+meanCol.ToString());  
            
        }

        mesh.vertices = meanPosition.ToArray();
        mesh.colors = meanColor.ToArray();

        return mesh;
    }

    
}
