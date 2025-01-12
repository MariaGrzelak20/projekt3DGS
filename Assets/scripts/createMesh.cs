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

    private ComputeBuffer vertexBuffer;


    void Start()
    {
        customMaterial.SetVector("_CameraPos", Camera.main.transform.position);

        points3DRead reader = gameObject.GetComponent<points3DRead>();

        List<points3DRead.splatPoint> pointList = reader.readPoints();
        points = new List<Vector3>();

        foreach (points3DRead.splatPoint spt in pointList) 
        {
        points.Add(spt.position);
        }

        mesh = new Mesh();

        // Create mesh and assign vertices
        mesh = new Mesh();
        //mesh.vertices = points.ToArray();
        
       // mesh.Clear();
        List<List<points3DRead.splatPoint>> listaPktSp = reader.FindClosestGroups(pointList);
        mesh = reader.meshFromPly(listaPktSp);
        
        // Generate indices
       // int[] indices = Enumerable.Range(0, points.Count).ToArray();
        int[] indices = Enumerable.Range(0, mesh.vertices.Length).ToArray();
        mesh.SetIndices(indices, MeshTopology.Points, 0);

        // Recalculate bounds
        mesh.RecalculateBounds();
        Debug.Log($"Mesh bounds: {mesh.bounds}");


        // Assign mesh to the MeshFilter
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        // Assign material to the MeshRenderer
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = customMaterial;

        foreach (Vector3 a in GetComponent<MeshFilter>().mesh.vertices)
        {
            Debug.Log(a);
        }

    }

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
}
