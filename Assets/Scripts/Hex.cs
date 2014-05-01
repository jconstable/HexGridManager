using UnityEngine;
using System.Collections;

public class Hex : MonoBehaviour {

    public Vector2 HexPosition;
    public HexModel HexModel { get; set; }
    public Material MaterialToUse;

    public void Start()
    {
        GameObject hex = new GameObject();
        hex.AddComponent("HexModel");
        HexModel = hex.GetComponent<HexModel>();
        hex.transform.parent = transform;
        hex.transform.localPosition = new Vector3(0, 0, 0);
        hex.renderer.material = MaterialToUse;
    }

	void Update () 
    {
	
	}
}
