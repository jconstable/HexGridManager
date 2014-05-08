using UnityEngine;
using System.Collections;

public class Hex : MonoBehaviour {

    public Vector2 HexPosition;
    public HexModel HexModel { get; set; }
    public Material MaterialToUse;
	public float GridSize = 1f;

    public void Start()
    {
        GameObject hex = new GameObject();
		HexModel = hex.AddComponent< HexModel > ();
		HexModel.GridSize = GridSize;
        hex.transform.parent = transform;
        hex.transform.localPosition = new Vector3(0, 0, 0);
        hex.renderer.material = MaterialToUse;
    }

	void Update () 
    {
	
	}
}
