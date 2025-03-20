using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using static splat;

public class points3DRead : MonoBehaviour
{
    // Start is called before the first frame update

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

    public List<splatPoint> readPointBin() 
    {
        List<splatPoint> listSplat = new List<splatPoint>();
        string filePath = Path.Combine(Application.streamingAssetsPath, plyFileName);

        if (File.Exists(filePath))
        {
            FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            BinaryReader reader = new BinaryReader(fs);
            
                // Odczytanie nag³ówka
                string header = "";
                while (true)
                {
                    string line = ReadLine(reader);
                    header += line + "\n";
                    if (line.StartsWith("end_header"))
                        break;
                }

                // Znalezienie liczby wierzcho³ków
                int vertexCount = FindVertexCount(header);
                if (pointNumberLimit == 0 || pointNumberLimit > vertexCount) { pointNumberLimit = 16000; }

                // Odczytanie danych binarnych (x, y, z, r, g, b)
                for (int i = 0; i < vertexCount; i++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    float r = reader.ReadByte();
                    float g = reader.ReadByte();
                    float b = reader.ReadByte();

                    Vector3 position = new Vector3(x, y, z);
                    UnityEngine.Color color = new Color(r, g, b,1);
                    splatPoint point = new splatPoint(position, color);
                    listSplat.Add(point);

                

                    if (i == pointNumberLimit) break;
                }

            
        }
        else
        {
            Debug.LogError("Nie ma pliku PLY w StreaminAssets.");
        }

        Debug.Log("Ilosc splatow w bardzo poczatkowym czytaniu parametrow: " + listSplat.Count);
        int it = 0;
        foreach (splatPoint s in listSplat) {
            if (it % 10 == 0)
            {
                Debug.Log("Bardzo poczatkowe czytanie parametrow: " + s.position.x + " " + s.position.y + " " + s.position.z);
                
            }
            it++;
        }

        return listSplat;
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
                        new Vector3(
                            float.Parse(tempArr[0]), 
                            float.Parse(tempArr[1]), 
                            float.Parse(tempArr[2])),
                        new UnityEngine.Color(
                            float.Parse(tempArr[3]), 
                            float.Parse(tempArr[4]), 
                            float.Parse(tempArr[5]), 
                            float.Parse(tempArr[6]))                      
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

    private static string ReadLine(BinaryReader reader)
    {
        List<byte> lineBytes = new List<byte>();
        byte b;
        while ((b = reader.ReadByte()) != 10) // Czytanie do nowej linii (LF = 10)
        {
            lineBytes.Add(b);
        }
        return Encoding.ASCII.GetString(lineBytes.ToArray());
    }

    private static int FindVertexCount(string header)
    {
        foreach (string line in header.Split('\n'))
        {
            if (line.StartsWith("element vertex"))
            {
                string[] parts = line.Split(' ');
                return int.Parse(parts[2]); // Liczba wierzcho³ków
            }
        }
        throw new Exception("Nie znaleziono liczby wierzcho³ków w nag³ówku PLY.");
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
