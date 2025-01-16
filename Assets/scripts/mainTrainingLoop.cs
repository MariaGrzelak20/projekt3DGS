using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static points3DRead;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

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

        int zeroCounter = 0;

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
           
            double[,] covariance = splatFunctions.getCovarianceMatrix(meanPosition,pointsForMatrix);


            // Tworzymy macierz z danych
            Matrix<double> sigmaMatrix = DenseMatrix.OfArray(covariance);

            // Rozk³ad wartoœci w³asnych
            var evd = sigmaMatrix.Evd(symmetricity: Symmetricity.Symmetric);

            // Macierz rotacji (R)
            Matrix<double> R = evd.EigenVectors;

            // Skala (S) - pierwiastki z wartoœci w³asnych
            Vector<double> eigenValues = evd.EigenValues.Real(); // Wartoœci w³asne

            Vector<double> scale = eigenValues.PointwiseSqrt();  // Pierwiastki


            Vector3 scaleVec = new Vector3(
                (float)scale[0],
                (float)scale[1],
                (float)scale[2]
                );

            

            Quaternion rotationQuat = ParseMatrixToQuaternion(R);


            splatList.Add(new splat.splatStruct(
                meanPosition,
                meanColor,
                1f,
                scaleVec,
                rotationQuat
                ));
        }

        Debug.Log("Liczba splatow:" + splatList.Count());
        Debug.Log("Liczba zer w scale:" + zeroCounter);

        foreach (splat.splatStruct spStr in splatList)
        {
            if (float.IsNaN(spStr.scale.x))
            {
                Debug.Log("Dane splata: " +
                    "\nPosition: " + spStr.position
                    + "\n S: " + spStr.scale +
                    "\n R: " + spStr.rotation +
                    "\nColor: " + spStr.sh +
                    "\nVisibility: " + spStr.splatOpacity
                     );
            }
        }

    }


    Quaternion ParseMatrixToQuaternion(Matrix<double> R)
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
}
