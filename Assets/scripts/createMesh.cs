using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;


[RequireComponent(typeof(MeshFilter))]
public class createMesh : MonoBehaviour
{
    Mesh mesh;
    List<Vector3> points;
    public Material customMaterial; // Drag your material here in the Inspector

    void Start()
    {
        customMaterial.SetVector("_CameraPos", Camera.main.transform.position);
        points3DRead reader = gameObject.GetComponent<points3DRead>();
        mesh = new Mesh();

        //Loading points and their colors from file
        List<points3DRead.splatPoint> pointList = reader.readPoints();
        

        //mesh = reader.meshFromPly(listaPktSp);
        mesh = meshFromSplatPoint(pointList);
        //mesh = meshFromSplatStruct();

        int[] indices = Enumerable.Range(0, mesh.vertices.Length).ToArray();
        mesh.SetIndices(indices, MeshTopology.Points, 0);

        // Recalculate bounds
        mesh.RecalculateBounds();
        //Debug.Log($"Mesh bounds: {mesh.bounds}");


        // Assign mesh to the MeshFilter
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;


        // Assign material to the MeshRenderer
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = customMaterial;

       /* foreach (Vector3 a in GetComponent<MeshFilter>().mesh.vertices)
        {
            Debug.Log(a);
        }
       */

        points = new List<Vector3>();

        //Loading only the cooridnates of points
       foreach (points3DRead.splatPoint spt in pointList)
        { 
            points.Add(spt.position);
        }


    }

    //Draw raw 3d points
    void OnDrawGizmos()
    {
        

        if (points != null)
        {
            Gizmos.color = Color.red;
            foreach (var point in points)
            {
                Gizmos.DrawSphere(point, 0.05f); // Adjust size as needed
            }
        }
    }

    /// <summary>
    /// To create mesh we use only splat points - points and color read from file, not splats
    /// </summary>
    /// <returns></returns>
    public Mesh meshFromSplatPoint(List<points3DRead.splatPoint> pointList) 
    {
        Mesh mesh = new Mesh();

        points3DRead reader = gameObject.GetComponent<points3DRead>();

        //Finding clusters of 3 closest to each other points. List holding Lists of three points
        List<List<points3DRead.splatPoint>> listaPktSp = reader.FindClosestGroups(pointList);

        mesh = reader.meshFromPly(listaPktSp);

        return mesh;
    }

    /// <summary>
    /// To create mesh we use splat struct, that is a structue describing splats
    /// </summary>
    /// <returns></returns>
    public Mesh meshFromSplatStruct()
    {
        Mesh mesh = new Mesh();

        return mesh;
    }
}
