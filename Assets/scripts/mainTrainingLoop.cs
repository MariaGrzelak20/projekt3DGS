using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static points3DRead;

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
    ComputeBuffer m_Buffer;
    

    void Start()
    {
        List<splat.splatStruct> splatList = new List<splat.splatStruct>();
        points3DRead reader = gameObject.AddComponent<points3DRead>();
        
        List<points3DRead.splatPoint> points = reader.readPoints();
        splat splatFunctions = gameObject.AddComponent<splat>();
        
        //scaling points by 1000, because most of them fall to 0,00... and float shows this as 0
        float scaleFactor = 100f;

        for (int i = 0; i < points.Count; i++) 
        {
            var p = points[i]; // Copy the struct (value type)
            p.position *= scaleFactor; // Modify the position
            points[i] = p; // Assign the modified struct back to the list
        }

        foreach (var point in points)
        {
            // Calculate distances from the current point to all other points
            var distances = points
                .Where(p => (p.position - point.position).sqrMagnitude > 1e-6f)
                .Select(p => new { Point = p, Distance = Vector3.Distance(point.position, p.position) })
                .OrderBy(x => x.Distance) // Sort by distance
                .Take(2) // Take the two closest points
                .Select(x => x.Point) // Get the points only
                .ToList();

            // Add the current point and its two closest points as a group
            var group = new List<splatPoint> { point };
            group.AddRange(distances);
            Vector3 meanPosition = ( group[0].position+ group[1].position + group[2].position) / 3;
            Color meanColor = (point.color + group[0].color + group[1].color) / 3;

            Vector3[] pointsForMatrix = {group[0].position , group[1].position , group[2].position };
           
            Vector3[] covMatrix = splatFunctions.getCovarianceMatrix(meanPosition,pointsForMatrix);

            splatList.Add(new splat.splatStruct(
                meanPosition,
                covMatrix,
                meanColor,
                1f
                ));
        }

        foreach (splat.splatStruct spStr in splatList) 
        {
            Debug.Log("Dane splata: "+
                "\nPosition: "+spStr.position+
                "\nCovMAtrix: " 
                +"\n "+ spStr.covMatrix[0]+
                "\n "+ spStr.covMatrix[1]+
                "\n "+ spStr.covMatrix[2]+
                "\nColor: " + spStr.sh+
                "\nVisibility: " + spStr.splatOpacity
                 );
        }

    }

    

}
