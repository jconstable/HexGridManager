// Copyright 2014 John Constable
//
// This is a component intended to be used within a Unity project. It creates and stores a map of
// heagonal grid square, and provides various functions such as validitiy testing, collision testing,
// and vacant neighbor searching. It is intended to be used in collaboration with Unity's built-in
// pathfinding/navmesh solution, although there are no true dependendencies. The coordinate system used
// by this hex grid is Axial.
//
// TODO: Distance testing, find best path
//
// HexGridManager is free software: you can redistribute it and/or modify it under the terms of the GNU General
// Public License as published by the Free Software Foundation, either version 3 of the License, or (at
// your option) any later version.
//
// HexGridManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details. http://www.gnu.org/licenses/.

using UnityEngine;
using System.Collections.Generic;

public class HexGridManager : MonoBehaviour
{
    protected static readonly IntVector2[] neighborVectors = new IntVector2[6]
    {
        new IntVector2( +1,  0 ),
        new IntVector2( +1, -1 ),
        new IntVector2( 0, -1 ),
        new IntVector2( -1,  0 ),
        new IntVector2( -1, +1 ),
        new IntVector2( 0, +1 )
    };

    // The size in Unity meters of the length of one grid square
    public float GridSize = 0.25f;
    // The length in squares of one side of the entire possible grid space. Must be Power of 2
    public int GridRowMax = 1024;

	// Allow configuration of debug visual Y position, to avoid Z-fighting
	public float UnoccupiedDebugYPosition = 0.001f;
	public float OccupiedDebugYPosition = 0.002f;

    // Triggers debug display, if prefabs are also present
    public bool ShowDebug = false;
    public bool ShowDebugLabel = false;

    public GameObject OccupiedTilePrefab = null;
    public GameObject DestinationTilePrefab = null;
    public Material debugHexVacantMaterial;

    // Leave as public so they will be serialized in the editor
    [HideInInspector]
    public List<int> validGrids = new List< int >();

    // Keep track of who is overlapping which grids
    protected Dictionary< int, List< int > > _occupantBuckets = new Dictionary<int, List< int > >();

    private List< InternalOccupant > _occupants = new List<InternalOccupant>();
    private List< GameObject > _debugVisuals = new List<GameObject>();

    // Store some numbers that are frequently used
    private int _exponent = 0;
    private int GridRowMaxHalf {
        get {
            if(_gridRowMaxHalf == 0)
                this.DetermineExponent();
            return _gridRowMaxHalf;
        }
        set {
            _gridRowMaxHalf = value;
        }
    }
    private int _gridRowMaxHalf = 0;

    private static readonly float _sqrtThree = Mathf.Sqrt(3f);
    private static readonly float _oneThird = (1f/3f);
    private static readonly float _twoThirds = (2f/3f);
    private static readonly float _threeHalves = (3f/2f);

    private GenericPool< List< int > > _intListPool = null;
    private GenericPool< InternalOccupant > _occupantPool = null;
    protected GenericPool< IntVector2 > _intVectorPool = null;

    private Mesh _debugMesh = null;

    // Use this for initialization
    public HexGridManager()
    {
        _intListPool = new GenericPool< List< int > >(CreateNewList);
        _occupantPool = new GenericPool< InternalOccupant >(CreateNewOccupant);
        _intVectorPool = new GenericPool< IntVector2 >(CreateNewIntVector);
    }

    // Return true if the vector is a hex coordinate on the navmesh
    // vec - A grid position, using x and z as the coordinates
    public bool IsValid( Vector3 vec )
    {
        return (validGrids.BinarySearch(GetGridSig(vec)) > -1);
    }

    // Returns true if the vectory is a hex coordinate that currently has an occupant
    // vec - A grid position, using x and z as the coordinates
    // params filter (optional) - Do not count the given occupants
    public bool IsOccupied( Vector3 vec, params object[] filters )
    {
        return IsOccupied(GetGridSig(vec), filters );
    }

	public bool IsOccupied( Vector3 vec )
	{
		return IsOccupied(GetGridSig(vec), null);
	}

    public void GetOccupants( Vector3 vec, List< GameObject > occupantsOut )
    {
        occupantsOut.Clear();

        int sig = GetGridSig( vec );
        List< int > occupants = null;
        bool exists = _occupantBuckets.TryGetValue(sig, out occupants);

        if( !exists )
        {
            return;
        }

        foreach( int occupantID in occupants )
        {
            foreach( InternalOccupant o in _occupants )
            {
                if( o.ID == occupantID )
                {
                    if( o.TrackedGameObject != null )
                    {
                        occupantsOut.Add( o.TrackedGameObject );
                    }
                }
            }
        }
    }

    // Convert from world space to grid space
    // pos - The world space vector
    // grid - The vector to which to write
    public void PositionToGrid( Vector3 pos, ref Vector3 outGrid )
    {
        using (IntVector2 igrid = _intVectorPool.GetObject())
        {
            PositionToGrid(pos, igrid);
            outGrid.x = igrid.x;
            outGrid.y = 0;
            outGrid.z = igrid.y;
        }
    }

    // Convert from a hex grid space to a world space position
    // grid - A grid position, using x and z as the coordinates
    // pos - The vector to write to
    public void GridToPosition( Vector3 grid, ref Vector3 pos )
    {
        GridToPosition(Mathf.RoundToInt(grid.x), Mathf.RoundToInt(grid.z), ref pos);
    }

    // Convert from a hex grid space to a world space position
    // intx - The X grid coordinate
    // inty - The Y grid coordinate
    // pos - The vector to write to write
    public void GridToPosition( int intx, int inty, ref Vector3 pos )
    {
        float x = (float)intx;
        float y = (float)inty;
        float g = (float)GridSize;

        pos.x = g * _sqrtThree * (x + ( y * 0.5f ) );
        pos.y = 0f;
        pos.z = g * _threeHalves * y;
    }

    public void SnapPositionToGrid( Vector3 pos, ref Vector3 snapPos )
    {
        Vector3 temp = Vector3.zero;
        PositionToGrid( pos, ref temp );
        GridToPosition( temp, ref snapPos );
    }

    // Find the nearest unoccupied grid position
    // dest - The object to which you'd like to find the closest unoccupied grid
    // outGrid - The vector to which coordinates will be written
    public void GetClosestVacantNeighbor( GameObject dest, ref Vector3 outGrid )
    {
        GetClosestVacantNeighbor(dest, ref outGrid, Vector3.zero);
    }

    // Find the nearest unoccupied grid position
    // dest - The object to which you'd like to find the closest unoccupied grid
    // outGrid - The vector to which coordinates will be written
    // dir - Unit vector specifying in which direction you'd like the returned grid to favor
    public void GetClosestVacantNeighbor( GameObject dest, ref Vector3 outGrid, Vector3 dir )
    {
        GetClosestVacantNeighbor( dest.transform.position, ref outGrid, dir );
    }

    public void GetClosestVacantNeighbor( Vector3 dest, ref Vector3 outGrid, Vector3 dir )
    {
        int currentSig = 0;

        // Early out if the given grid is unoccupied
        using (IntVector2 occupantCurrent = _intVectorPool.GetObject())
        {
            PositionToGrid(dest, occupantCurrent);
            currentSig = GetGridSig(occupantCurrent);

            bool occupied = IsOccupied(currentSig);
            if (!occupied)
            {
                outGrid.Set(occupantCurrent.x, 0, occupantCurrent.y);
                return;
            }
        }

        int magnitude = 1;
        bool found = false;

        while( !found )
        {
            List< int > unoccupiedNeighbors = AcquireListOfUnoccupiedNeighbors(currentSig, magnitude, true);
            using (IntVector2 nearest = _intVectorPool.GetObject())
            {
                nearest.Set(0,0); // Initialize, just in case

                if (unoccupiedNeighbors.Count > 0)
                {
                    // Now that we have a list of unoccupied neighbors, find the one in the best direction
                    if (dir.sqrMagnitude < Mathf.Epsilon)
                    {
                        // Direction doesn't matter
                        SigToGrid(unoccupiedNeighbors [0], nearest);
                    }
                    else
                    {
                        // Direction does matter
                        FindNeighborClosestToPoint(dest, dir, unoccupiedNeighbors, nearest);
                    }
                    found = true;
                }

                outGrid.Set(nearest.x, 0, nearest.y);
            }

            unoccupiedNeighbors.Clear ();
            _intListPool.ReturnObject(unoccupiedNeighbors);

            magnitude++;
        }
    }

    public List<Vector3> GetAllWorldPositions()
    {
        List<Vector3> positions = new List<Vector3>();
        Vector3 wPos = Vector3.zero;

        using(IntVector2 vec = _intVectorPool.GetObject())
        {
            foreach(int sig in validGrids)
            {
                SigToGrid(sig, vec);

                GridToPosition(vec.x, vec.y, ref wPos);

                positions.Add(wPos);
            }
        }

        return positions;
    }

    public Vector3[] GetNeighborPositions(Vector3 worldPosition)
    {
        IntVector2 grid = _intVectorPool.GetObject();
        PositionToGrid(worldPosition, grid);

        int sig = GetGridSig(grid);

        List<int> neighbors = AcquireListOfNeighbors(sig, false);
        Vector3[] neighborPositions = new Vector3[neighbors.Count];
        for(int i = 0; i < neighbors.Count; ++i)
        {
            IntVector2 nGrid = _intVectorPool.GetObject();
            SigToGrid(neighbors[i], nGrid);

            Vector3 nPos = Vector3.zero;
            GridToPosition(nGrid.x, nGrid.y, ref nPos);

            neighborPositions[i] = nPos;
        }

        return neighborPositions;
    }

    #region Occupants
    // Create an occupant on the grid with a given magnitude (number of rings to occupy)
    // Occupants track the position of the given GameObject, and will send the "OnGridChanged" message
    // to the GameObject if its grid position changes.
    // thing - The GameObject that this Occupant is tracking
    // magnitude - The number of rings to occupy around the central hex
    public Occupant CreateOccupant( GameObject thing, int magnitude )
    {
        InternalOccupant o = CreateInternalOccupant( thing.transform.position, thing, magnitude );
        return (Occupant)o;
    }

    // Return the Occupant to the manager
    // occupant - The Occupant that is being returned
    public void ReturnOccupant( ref Occupant occupant )
    {
        ReturnInternalOccupant (occupant as InternalOccupant);
        occupant = null;
    }
    #endregion

    #region Reservations
    // Create a reservation on a single grid space in the grid
    // pos - The position in world space of the grid you want to reserve
    public Reservation CreateReservation( Vector3 pos )
    {
        InternalOccupant o = CreateInternalOccupant( pos, null, 0 );
        return (Reservation)o;
    }

    // Return the Reservation to the manager
    // reservation - The reservation being returned
    public void ReturnReservation( ref Reservation reservation )
    {
        ReturnInternalOccupant (reservation as InternalOccupant);
        reservation = null;
    }
    #endregion

    #region Distance
    public int GridDistanceBetweenOccupants( Occupant a, Occupant b, int maxDistance )
    {
        int distance = 0;

        InternalOccupant occupantA = a as InternalOccupant;
        InternalOccupant occupantB = b as InternalOccupant;
        if(occupantA == null || occupantB == null)
        {
            Debug.LogWarning("One of these occupants is null wtf!");
        }
        if( occupantA.TrackedGameObject == null || occupantB.TrackedGameObject == null )
        {
            Debug.LogWarning( "Unable to calculate grid distances between two occupants that do not track GameObjects" );
            return -1;
        }

        distance = GridDistanceBetweenVectors( occupantA.TrackedGameObject.transform.position, occupantB.TrackedGameObject.transform.position, maxDistance );

        return distance;
    }

    public int GridDistanceBetweenVectors( Vector3 a, Vector3 b, int maxDistance )
    {
        int distance = 0;
        int ax, ay, bx, by;

        PositionToGrid( a, out ax, out ay );
        PositionToGrid( b, out bx, out by );

        distance = ( Mathf.Abs( ax - bx ) + Mathf.Abs( ay - by ) + Mathf.Abs( ax + ay - bx - by ) ) / 2;
        distance = Mathf.Min( distance, maxDistance );

        return distance;
    }
    #endregion





    #region Private
    private List< int > CreateNewList()
    {
        return new List<int>();
    }

    private InternalOccupant CreateNewOccupant()
    {
        return new InternalOccupant(this);
    }

    private IntVector2 CreateNewIntVector()
    {
        return new IntVector2( this );
    }

    private void DetermineExponent()
    {
        while (( GridRowMax >> _exponent ) != 1)
        {
            _exponent++;
        }

        GridRowMaxHalf = GridRowMax / 2;
    }

	private List< InternalOccupant > _occupantsToMessage = new List< InternalOccupant > ();

    void Awake()
    {
        DetermineExponent();
    }

    // Update is called once per frame
    void Update ()
    {
        // Process occupants and signal if they have moved to a new grid
        using (IntVector2 grid = _intVectorPool.GetObject())
        {
            foreach (InternalOccupant occupant in _occupants)
            {
                // Only update tracking for occupants that have GameObjects
                if (occupant.TrackedGameObject != null)
                {
                    PositionToGrid(occupant.TrackedGameObject.transform.position, grid);
                    int sig = GetGridSig(grid);

                    // See if it's moved
                    if (sig != occupant.Sig)
                    {
                        occupant.Update(grid, OccupiedTilePrefab);

						_occupantsToMessage.Add( occupant );
                    }
                }
            }

			foreach (InternalOccupant occupant in _occupantsToMessage) 
			{
				occupant.TrackedGameObject.SendMessage( "OnGridChanged", SendMessageOptions.DontRequireReceiver );
			}
			_occupantsToMessage.Clear ();
        }

        ToggleDebugVisuals();
    }

    // Rebuild the hex grids from the NavMesh (not recommended at runtime)
    public void RebuildFromNavMesh()
    {
        validGrids.Clear();
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        FillValidGrids();
        stopwatch.Stop();
        Debug.Log("GridManager: Setup took " + stopwatch.ElapsedMilliseconds + "ms" );
    }

    // Report some stats to the console
    public void ReportStats()
    {
        _intListPool.PrintStats();
        _intVectorPool.PrintStats();
        _occupantPool.PrintStats();
        Debug.Log("Hex spaces on NavMesh: " + validGrids.Count);
    }

    private void ToggleDebugVisuals()
    {
        if (!ShowDebug && _debugVisuals.Count > 0)
        {
            foreach (InternalOccupant occ in _occupants)
            {
                occ.DestroyVisuals();
            }
        }
    }

    private void PositionToGrid( Vector3 pos, IntVector2 grid )
    {
        PositionToGrid( pos, out grid.x, out grid.y );
    }

    private void PositionToGrid( Vector3 pos, out int x, out int y )
    {
        float g = (float)GridSize;
        float oneOverG = 1f / g;

        float q = (_oneThird * ( _sqrtThree *  pos.x )) - (_oneThird * pos.z );
        q *= oneOverG;
        float r = (_twoThirds * pos.z) * oneOverG;

        // XA: This is actually a rough estimate...
        x = Mathf.RoundToInt( q );
        y = Mathf.RoundToInt( r );

        // XA: Acquire a list of neibhors, including ourself, and check which one is actually closer
        int currentSig = GetGridSig(x, y);
        List< int > neighbors = this.AcquireListOfNeighbors(currentSig, true);

        int closestSig = -1;
        float closestDist = Mathf.Infinity;
        foreach(int n in neighbors)
        {
            int nx = 0, ny = 0;
            this.SigToGrid(n, out nx, out ny);

            // Calculate distance from our translate "grid coordinates" and the center of this hex
            float distance = ( Mathf.Abs( nx - q ) + Mathf.Abs( ny - r ) + Mathf.Abs( nx + ny - q - r ) ) / 2.0f;
            if(distance < closestDist)
            {
                closestSig = n;
                closestDist = distance;
            }
        }

        using (IntVector2 closestPos = _intVectorPool.GetObject())
        {
            SigToGrid(closestSig, closestPos);

            x = closestPos.x;
            y = closestPos.y;
        }
    }

    private bool IsOccupied( int sig, params object[] filters )
    {
        List< int > occupants = null;
        bool exists = _occupantBuckets.TryGetValue(sig, out occupants);

        if( !exists )
        {
            return false;
        }

        int numOccupants = occupants.Count;
        for (int i = 0; i < filters.Length; ++i) {
            InternalOccupant occupant = filters [i] as InternalOccupant;
            if (occupant != null && occupants.Contains (occupant.ID)) {
                numOccupants--;
            }
        }

        return (numOccupants > 0);
    }



    private int GetGridSig( Vector3 vec )
    {
        return GetGridSig( Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.z) );
    }

    private int GetGridSig( IntVector2 vec )
    {
        return GetGridSig(vec.x, vec.y);
    }

    private int GetGridSig( int x, int y )
    {
        return ( x + ( GridRowMaxHalf ) ) +
            ( ( y + ( GridRowMaxHalf ) ) << _exponent);
    }

    private void SigToGrid( int sig, IntVector2 vec )
    {
        SigToGrid( sig, out vec.x, out vec.y );
    }

    private void SigToGrid( int sig, out int x, out int y )
    {
        x = ( sig % GridRowMax ) - ( GridRowMaxHalf );
        sig >>= _exponent;
        y = ( sig % GridRowMax ) - ( GridRowMaxHalf );
    }

    private InternalOccupant CreateInternalOccupant( Vector3 pos, GameObject tracked, int magnitude )
    {
        InternalOccupant o = _occupantPool.GetObject();

        using (IntVector2 grid = _intVectorPool.GetObject())
        {
            PositionToGrid(pos, grid);

            o.Track(tracked);
            o.SetMagnitude(magnitude);
            o.Update(grid, (tracked == null) ? DestinationTilePrefab : OccupiedTilePrefab );
        }

        _occupants.Add(o);

        return o;
    }

    private void ReturnInternalOccupant( InternalOccupant occupant )
    {
        _occupants.Remove(occupant);

        occupant.DestroyVisuals();
        occupant.Vacate();
        occupant.Track(null);

        _occupantPool.ReturnObject(occupant);
    }

    private void PushNewMagnitudeOfNeighborsIntoStack( List<int> actedGrids, Stack<int> neighbors )
    {
        using (IntVector2 actedVec = _intVectorPool.GetObject())
        {
            using (IntVector2 neighborVec = _intVectorPool.GetObject())
            {
                foreach (int acted in actedGrids)
                {
                    for (int i = 0; i < neighborVectors.Length; i++)
                    {
                        SigToGrid(acted, actedVec);

                        neighborVec.Set(actedVec.x + neighborVectors [i].x,
                                        actedVec.y + neighborVectors [i].y);

                        int neighborSig = GetGridSig(neighborVec);
                        if (!neighbors.Contains(neighborSig) && !actedGrids.Contains(neighborSig))
                        {
                            neighbors.Push(neighborSig);
                        }
                        //else
                        //{
                        //    using( IntVector2 g = _intVectorPool.GetObject() )
                        //    {
                        //        SigToGrid( neighborSig, g );
                        //        Debug.Log( "Sig Failed " + neighborSig + " " + g.ToString() );
                        //    }
                        //}
                    }
                }
            }
        }
    }

    private void FindNeighborClosestToPoint( Vector3 center, Vector3 dir, List<int> neighbors, IntVector2 closestNeighbor )
    {
        // Initialize the out vector just in case
        closestNeighbor.Set(0, 0);

        int x, y;

        float lastDot = float.MinValue;
        foreach( int neighborSig in neighbors )
        {
            SigToGrid( neighborSig, out x, out y );
            Vector3 neighborPos = Vector3.zero;

            GridToPosition( x, y, ref neighborPos );

            float dot = Vector3.Dot( dir, ( center - neighborPos ).normalized );
            if( dot > lastDot )
            {
                closestNeighbor.Set( x, y );
                lastDot = dot;
            }
        }
    }

    private List< int > AcquireListOfNeighbors( int currentSig, bool includeSelf = false )
    {
        List< int > neighbors = _intListPool.GetObject();
        neighbors.Clear();
        neighbors.Capacity = neighborVectors.Length + ( includeSelf ? 0 : 1 );

        int x, y;
        SigToGrid( currentSig, out x, out y );

        if( includeSelf )
        {
            neighbors.Add( currentSig );
        }

        for( int i = 0; i < neighborVectors.Length; ++i )
        {
            int neighborSig = GetGridSig( x + neighborVectors[i].x, y + neighborVectors[i].y );
            neighbors.Add( neighborSig );
        }

        return neighbors;
    }

    private List< int > AcquireListOfNeighbors( int currentSig, int magnitude, bool includeSelf = false )
    {
        List< int > neighbors = _intListPool.GetObject();
        neighbors.Clear();

        //magnitude = Mathf.Max( 1, magnitude );
        neighbors.Capacity = neighborVectors.Length * magnitude + ( includeSelf ? 0 : 1 );

        int x, y;
        SigToGrid( currentSig, out x, out y );

        if( includeSelf )
        {
            neighbors.Add( currentSig );
        }

        for( int k = 1; k <= magnitude; ++k )
        {
            for( int i = 0; i < neighborVectors.Length; ++i )
            {
                for( int j = 0; j < k; ++j )
                {
                    int neighborSig = GetGridSig( x + neighborVectors[i].x * k, y + neighborVectors[i].y * k );
                    neighbors.Add( neighborSig );
                }
            }
        }

        return neighbors;
    }

    private List< int > AcquireListOfUnoccupiedNeighbors( int currentSig, int magnitude, bool includeSelf = false )
    {
        List< int > neighbors = AcquireListOfNeighbors( currentSig, magnitude, false );
        if( includeSelf )
        {
            neighbors.Add( currentSig );
        }
        neighbors.RemoveAll( ( int sig ) => { return IsOccupied( sig ); } );

        return neighbors;
    }
    #endregion

    void FillValidGrids()
    {
        if( !Mathf.IsPowerOfTwo( GridRowMax ) )
        {
            Debug.LogError( "GridManager: Invalid GridRowMax, must be Power of Two" );
            return;
        }

        DetermineExponent();

        Dictionary< int, bool > triedValues = new Dictionary< int, bool >();
        bool b;

        int maxStackSize = 0;
        NavMeshHit hit;
        Vector3 pos = new Vector3();

        Stack< int > neighborsToTry = new Stack< int >();

        // Start it off
        using (IntVector2 grid = _intVectorPool.GetObject())
        {
            grid.Set(0, 0);
            neighborsToTry.Push(GetGridSig(grid));

            int sig = 0;
			float minGridSizeForTesting = Mathf.Max( 0.2f, GridSize / 2f );

            using (IntVector2 tmpGrid2 = _intVectorPool.GetObject())
            {
                while (neighborsToTry.Count > 0)
                {
                    maxStackSize = Mathf.Max(maxStackSize, neighborsToTry.Count);

                    sig = neighborsToTry.Pop();
                    SigToGrid(sig, grid);

                    triedValues.Add(sig, true);

                    GridToPosition(grid.x, grid.y, ref pos);
                    if (NavMesh.SamplePosition(pos, out hit, minGridSizeForTesting, -1))
                    {

                        //if( hit.distance <= minGridSizeForTesting )
                        {
                            validGrids.Add(sig);

                            for (int i = 0; i < HexGridManager.neighborVectors.Length; i++)
                            {
                                IntVector2 neighborDir = HexGridManager.neighborVectors [i];

                                tmpGrid2.Set(neighborDir.x + grid.x, neighborDir.y + grid.y);
                                int nextSig = GetGridSig(tmpGrid2);
                                if (!triedValues.TryGetValue(nextSig, out b) && !neighborsToTry.Contains(nextSig))
                                {
                                    neighborsToTry.Push(nextSig);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Sort so we can bsearch it
        validGrids.Sort();

        if (validGrids.Count == 0)
        {
            Debug.LogError("Unable to find any possible grid units in this scene. Make sure you have baked a nav mesh, and that the nav mesh includes the world origin (0,0)");
            return;
        }

        Debug.Log("Hex spaces on NavMesh found: " + validGrids.Count);
        Debug.Log("Max stack size during search: " + maxStackSize);
        _intVectorPool.PrintStats();
    }

    private void OnDrawGizmos()
    {
        if(_debugMesh == null) {
            DetermineExponent();
            _debugMesh = this.CreateHexMesh();
        }

        if(_debugMesh != null && debugHexVacantMaterial != null)
        {
            debugHexVacantMaterial.SetPass(0);

            using(IntVector2 grid = _intVectorPool.GetObject())
            {
                foreach (int sig in validGrids)
                {
                    SigToGrid(sig, grid);

                    Vector3 pos = Vector3.zero;
                    GridToPosition( grid.x, grid.y, ref pos );
                    pos += Vector3.up * UnoccupiedDebugYPosition;

                    Graphics.DrawMeshNow( _debugMesh, pos, Quaternion.identity );

#if UNITY_EDITOR
                    if( ShowDebugLabel ) UnityEditor.Handles.Label( pos, "[" + grid.x + "," + grid.y + "]" );
#endif

                }
            }
        }
    }

    private Mesh CreateHexMesh()
    {
        float Radius = GridSize;
        float HalfWidth = (float)Mathf.Sqrt((Radius * Radius) - ((Radius / 2.0f) * (Radius / 2.0f)));

        Vector3[] normals = new Vector3[]
        {
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 0)
        };

        int[] tris = new int[]
        {
            0, 6, 1, 1, 6, 2, 2, 6, 3, 3, 6, 4, 4, 6, 5, 5, 6, 0
        };

        Vector3[] Vertices = new Vector3[7];
        Color[] Colors = new Color[7];
        Vector2[] UV = new Vector2[7];

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

        // Create the mesh
        Mesh mesh = new Mesh { name = "Hex Mesh" };
        mesh.vertices = Vertices;
        mesh.colors = Colors;
        mesh.uv = UV;
        mesh.SetTriangles(tris, 0);
        mesh.normals = normals;

        return mesh;
    }

    #region InnerClasses
    // Simple container to remove need for a normal Vector2 for holding grid coordinates
    public class IntVector2 : System.IDisposable
    {
        public int x;
        public int y;

        private HexGridManager _manager;

        public IntVector2( HexGridManager manager ) { _manager = manager; x = 0; y = 0; }
        public IntVector2( int _x, int _y ) { _manager = null; x = _x; y = _y; }

        public void Set( IntVector2 other ) { x = other.x; y = other.y; }
        public void Set( int _x, int _y ) { x = _x; y = _y; }

        public bool Equals( IntVector2 other )
        {
            return (x == other.x) && (y == other.y);
        }

        public int SqrMagnitude
        {
            get { return x * x + y * y; }
        }

        public void Dispose()
        {
            if (_manager != null)
            {
                _manager._intVectorPool.ReturnObject( this );
            }
        }

        public override string ToString()
        {
            return "[" + x + ", " + y + "]";
        }
    }

    // Public interface to allow users of this class to reference their Occupants without
    // access to the implementation
    public interface Occupant {
    }

    // Public interface to allow users of this class to reference their Reservations without
    // access to the implementation
    public interface Reservation {
    }

    // Class that ecapsulates a 2D region of occupied grid squares
    protected class InternalOccupant : Occupant, Reservation
    {
        // Getter for the occupant ID
        public int ID { get { return _id; } }

        // Getter for the current sig
        public int Sig { get { return _current; } }

        // Getter for the tracked GameObject
        public GameObject TrackedGameObject { get { return _trackedGameObject; } }

        // Static to uniquely identify every Occupant that is created
        private static int IdCounter = 0;

        private int _id = -1;                                       // Unique ID
        private HexGridManager _manager;                            // Reference to the parent grid manager
        private GameObject _trackedGameObject = null;               // The GameObject that this Occupant is tracking
        private int _current;                                       // Last known coordinates of this occupant
        private int _magnitude;                                     // The extent to which this Occupant extends from center
        private int _debugTileCounter = 0;
        private List< GameObject > debugVisuals = new List<GameObject>();

        public InternalOccupant( HexGridManager manager )
        {
            _manager = manager;
            _id = IdCounter++;
        }

        // Set the magnitude for the occupant
        public void SetMagnitude( int magnitude )
        {
            _magnitude = magnitude;
            DestroyVisuals();
        }

        public void DestroyVisuals()
        {
            foreach (GameObject o in debugVisuals) { Destroy(o); }
            debugVisuals.Clear();
            _debugTileCounter = 0;
        }

        public void Track( GameObject go )
        {
            _trackedGameObject = go;
        }

        // Mark the grid with this occupant's new coordinates
        public void Update( IntVector2 vec, GameObject debugPrefab )
        {
            Vacate();

            _current = _manager.GetGridSig(vec);

            // Stamp this occupant into the grid
            Occupy( debugPrefab );
        }

        // Add this occupants area to the grid
        public void Occupy( GameObject debugPrefab )
        {
            _debugTileCounter = 0; // More straighforward counter for which tile is being updated within UpdateDebugVisuals

            // List of grid sigs we have visited
            List< int > actedGrids = _manager.AcquireListOfNeighbors( _current, _magnitude, true );

            foreach( int sig in actedGrids )
            {
                AddFootprintToGrid( sig );
                UpdateDebugVisuals( false, sig, debugPrefab );
            }

            actedGrids.Clear();
            _manager._intListPool.ReturnObject(actedGrids);
        }

        // Remove this occupants area from the grid
        public void Vacate()
        {
            _debugTileCounter = 0; // More straighforward counter for which tile is being updated within UpdateDebugVisuals

            // List of grid sigs we have visited
            List< int > actedGrids = _manager.AcquireListOfNeighbors( _current, _magnitude, true );
            foreach( int sig in actedGrids )
            {
                RemoveFootprintFromGrid( sig );
                UpdateDebugVisuals( true, sig, null );
            }

            actedGrids.Clear();
            _manager._intListPool.ReturnObject(actedGrids);
        }

        private void AddFootprintToGrid( int sig )
        {
            List< int > bucket = null;
            if( _manager._occupantBuckets.TryGetValue( sig, out bucket ) )
            {
                if( !bucket.Contains( _id ) )
                {
                    bucket.Add( _id );
                }
            } else {
                bucket = _manager._intListPool.GetObject();
                bucket.Add( _id );
                _manager._occupantBuckets.Add( sig, bucket );
            }
        }

        private void RemoveFootprintFromGrid( int sig )
        {
            List< int > bucket = null;
            if( _manager._occupantBuckets.TryGetValue( sig, out bucket ) )
            {
                bucket.Remove( _id );

                if( bucket.Count == 0 )
                {
                    bucket.Clear();
                    _manager._intListPool.ReturnObject( bucket );
                    _manager._occupantBuckets.Remove( sig );
                }
            }
        }

        private void UpdateDebugVisuals( bool remove, int sig, GameObject debugPrefab )
        {
            if( !remove && _manager.ShowDebug && _manager.OccupiedTilePrefab != null )
            {
                // Attempt to reuse a grid
                if( _debugTileCounter >= debugVisuals.Count )
                {
                    GameObject newVisual = Instantiate( debugPrefab ) as GameObject;
					Hex hex = newVisual.GetComponent< Hex >();
					if( hex != null )
					{
                        float factor = ( _trackedGameObject == null ) ? 0.8f : 0.9f;
						hex.GridSize = _manager.GridSize * factor;
					}
                    newVisual.transform.parent = _manager.transform;
                    debugVisuals.Add( newVisual );
                }

                int x, y;
                Vector3 pos = Vector3.zero;

                _manager.SigToGrid( sig, out x, out y );
                _manager.GridToPosition( x, y, ref pos );

				debugVisuals[ _debugTileCounter ].transform.position = pos + (Vector3.up * _manager.OccupiedDebugYPosition);

				// Re-use pos
                pos.Set( x, 0, y );
                debugVisuals[ _debugTileCounter ].gameObject.SetActive( _manager.IsValid( pos ) );

                _debugTileCounter++;
            }
        }
    }

    // Simple class to support templated object pooling
    protected class GenericPool<T> {
        private Stack< T > _pool = new Stack< T >();

        public delegate T CreateObject();

        private CreateObject _func = null;

        private int _outstanding = 0;
        private int _highwater = 0;
        public GenericPool( CreateObject func )
        {
            _func = func;
        }

        ~GenericPool()
        {
            _pool.Clear();
        }

        public T GetObject()
        {
            T obj = default(T);

            if (_pool.Count > 0) {
                obj = _pool.Pop();
            } else {
                _highwater++;
                obj = _func();
            }
            _outstanding++;

            return obj;
        }

        public void ReturnObject( T obj )
        {
            _outstanding--;
            _pool.Push(obj);
        }

        public void PrintStats()
        {
            Debug.Log("GenericPool stats: (" + typeof(T).ToString() + "): " + _pool.Count + " in queue, " + _outstanding + " outstanding, "+_highwater+" max");
        }
    };
    #endregion
}
