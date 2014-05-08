using UnityEngine;
using System.Collections;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexModel : MonoBehaviour
{
    public Hex Parent;
    public Vector3[] Vertices;
	public Color[] Colors;
    public Vector3[] Normals;
    public Vector2[] UV;
    public int[] Triangles;

	public float GridSize = 0f;
    private float Radius = 0f;
	private float HalfWidth = 0f;

	void Start () 
    {
        Radius = GridSize;
        HalfWidth = (float)Mathf.Sqrt((Radius * Radius) - ((Radius / 2.0f) * (Radius / 2.0f)));

        Parent = transform.parent.GetComponent<Hex>();
        Vertices = new Vector3[7];
		Colors = new Color[7];
        UV = new Vector2[7];

        DrawTopAndBottom();
        SetTriangles();

        var mesh = new Mesh { name = "Hex Mesh" };
        GetComponent<MeshFilter>().mesh = mesh;
        mesh.vertices = Vertices;
		mesh.colors = Colors;
        mesh.uv = UV;
		mesh.SetTriangles(Triangles, 0);
        mesh.normals = Normals;
	}
	
	void Update () 
    {
	
	}

    #region draw
    private void SetTriangles()
    {
        Normals = new Vector3[]
            {
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
				new Vector3(0, 1, 0)
            };

        Triangles = new int[]
            {
                0, 6, 1, 1, 6, 2, 2, 6, 3, 3, 6, 4, 4, 6, 5, 5, 6, 0
            };
    }

    private void DrawTopAndBottom()
    {
        //top
        Vertices[0] = new Vector3(0, 0, -GridSize);
		Colors[0] = new Color(1, 0, 0);
		UV[0] = new Vector2(0.5f, 1);
        //topright
        Vertices[1] = new Vector3(HalfWidth, 0, -GridSize / 2);
		Colors[1] = new Color(1, 0, 0);
        UV[1] = new Vector2(1, 0.75f);
        //bottomright
        Vertices[2] = new Vector3(HalfWidth, 0, GridSize / 2);
		Colors[2] = new Color(1, 0, 0);
        UV[2] = new Vector2(1, 0.25f);
        //bottom
        Vertices[3] = new Vector3(0, 0, GridSize);
		Colors[3] = new Color(1, 0, 0);
        UV[3] = new Vector2(0.5f, 0);
        //bottomleft
        Vertices[4] = new Vector3(-HalfWidth, 0, GridSize / 2);
		Colors[4] = new Color(1, 0, 0);
        UV[4] = new Vector2(0, 0.25f);
        //topleft
        Vertices[5] = new Vector3(-HalfWidth, 0, -GridSize / 2);
		Colors[5] = new Color(1, 0, 0);
        UV[5] = new Vector2(0, 0.75f);
		// center
		Vertices[6] = new Vector3(0, 0, 0);
		Colors[6] = new Color(0, 0, 0);
		UV[6] = new Vector2(0.5f, 0.5f);

    }

    #endregion
}
