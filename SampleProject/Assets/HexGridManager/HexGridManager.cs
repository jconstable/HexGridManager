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
// GNU General Public License for more details.
//    
// You should have received a copy of the GNU General Public License along with Foobar. If not, 
// see http://www.gnu.org/licenses/.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Execute in edit mode to enable the rebuild checkbox
[ExecuteInEditMode]
public class HexGridManager : MonoBehaviour 
{
    protected static readonly IntVector2[] neighborVectors = new IntVector2[6] {
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

    // Triggers debug display, if prefabs are also present
    public bool ShowDebug = false;

    public GameObject VacantTilePrefab = null;
    public GameObject OccupiedTilePrefab = null;

    // Leave as public so they will be serialized in the editor
    [HideInInspector]
    public List<int> validGrids = new List< int >();

    // Keep track of who is overlapping which grids
    protected Dictionary< int, List< int > > _occupantBuckets = new Dictionary<int, List< int > >();

    private List< InternalOccupant > _occupants = new List<InternalOccupant>();
    private List< GameObject > _debugVisuals = new List<GameObject>();

    // Store some numbers that are frequently used
    private int _exponent = 0;
    private int _gridRowMaxHalf = 0;
    private static readonly float _sqrtThree = Mathf.Sqrt(3f);
    private static readonly float _oneThird = (1f/3f);
    private static readonly float _twoThirds = (2f/3f);
    private static readonly float _threeHalves = (3f/2f);

    private GenericPool< List< int > > _intListPool = null;
    private GenericPool< InternalOccupant > _occupantPool = null;
    protected GenericPool< IntVector2 > _intVectorPool = null;

    // Use this for initialization
    public HexGridManager () {
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
    // optionalFilter (optional) - Do not count the given occupant
    public bool IsOccupied( Vector3 vec, Reservation optionalFilter = null )
    {
        return IsOccupied(GetGridSig(vec), optionalFilter);
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
    // pos - The vector to write to write
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
        
        pos.x = g * _sqrtThree * (x + ( y / 2.0f ) );
        pos.y = 0f;
        pos.z = g * _threeHalves * y;
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
        int currentSig = 0;

        // Early out if the given grid is unoccupied
        using (IntVector2 occupantCurrent = _intVectorPool.GetObject())
        {
            PositionToGrid(dest.transform.position, occupantCurrent);
            currentSig = GetGridSig(occupantCurrent);
            if (!IsOccupied(currentSig))
            {
                outGrid.Set(occupantCurrent.x, 0, occupantCurrent.y);
                return;
            }
        }

        List< int > unoccupiedNeighbors = AcquireListOfUnoccupiedNeighbors(currentSig);      
        using (IntVector2 nearest = _intVectorPool.GetObject())
        {
            nearest.Set(0,0); // Initialize, just in case

            if (unoccupiedNeighbors.Count > 0)
            {
                // Now that we have a list of unoccupied neighbors, find the one in the best direction
                if (dir.sqrMagnitude == 0)
                {
                    // Direction doesn't matter
                    SigToGrid(unoccupiedNeighbors [0], nearest);
                } else
                {
                    // Direction does matter
                    FindNeighborClosestToPoint(dest.transform.position, dir, unoccupiedNeighbors, nearest);
                }
            }
            
            outGrid.Set(nearest.x, 0, nearest.y);
        }

        _intListPool.ReturnObject(unoccupiedNeighbors);
    }

    #region Occupants
    // Create an occupant on the grid with a given magnitude (number of rings to occupy)
    // Occupants track the position of the given GameObject, and will send the "OnGridChanged" message
    // to the GameObject if its grid position changes.
    // thing - The GameObject that this Occupant is tracking
    // magnitude - The number of rings to occupy around the central hex
    public Occupant CreateOccupant( GameObject thing, int magnitude )
    {
        return (Occupant)CreateInternalOccupant( thing.transform.position, thing, magnitude );
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
        return (Reservation)CreateInternalOccupant( pos, null, 0 );
    }

    // Return the Reservation to the manager
    // reservation - The reservation being returned
    public void ReturnReservation( ref Reservation reservation )
    {
        ReturnInternalOccupant (reservation as InternalOccupant);
        reservation = null;
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

        _gridRowMaxHalf = GridRowMax / 2;
    }

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
                        occupant.Occupy(true);
                        occupant.Update(grid);
                
                        occupant.TrackedGameObject.SendMessage("OnGridChanged");
                    }
                }
            }
        }

        if (Application.isPlaying)
        {
            ToggleDebugVisuals();
        }
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
        if( ShowDebug && VacantTilePrefab != null && _debugVisuals.Count == 0)
        {
            using( IntVector2 grid = _intVectorPool.GetObject())
            {
                foreach (int sig in validGrids)
                {
                    
                    SigToGrid(sig, grid);
                    
                    GameObject o = Instantiate(VacantTilePrefab) as GameObject;
                    Vector3 pos = Vector3.zero;
                    GridToPosition( grid.x, grid.y, ref pos );
                    o.transform.position = pos + ( Vector3.up * 0.001f );
                    o.transform.parent = transform;
                    _debugVisuals.Add(o);
                }
            }
        }
        
        if (!ShowDebug && _debugVisuals.Count > 0)
        {
            DestroyVisuals();
            foreach (InternalOccupant occ in _occupants)
            {
                occ.DestroyVisuals();
            }
        }
    }
    
    protected void DestroyVisuals()
    {
        for( int i = 0; i < _debugVisuals.Count; i++ )
        {
            GameObject o = _debugVisuals[i];
            Destroy( o );
        }
        
        _debugVisuals.Clear();
    }

    private void PositionToGrid( Vector3 pos, IntVector2 grid )
    {
        float g = (float)GridSize;
        
        float q = (_oneThird * ( _sqrtThree *  pos.x )) - (_oneThird * pos.z );
        q /= g;
        float r = (_twoThirds * pos.z) / g;
        
        grid.x = Mathf.RoundToInt( q );
        grid.y = Mathf.RoundToInt( r );
    }

    private bool IsOccupied( int sig, Reservation optionalFilter = null )
    {
        List< int > occupants = null;
        bool exists = _occupantBuckets.TryGetValue(sig, out occupants);
        
        if( optionalFilter == null || !exists )
        {
            return exists;
        }
        
        InternalOccupant occupant = optionalFilter as InternalOccupant;
        return !occupants.Contains(occupant.ID);
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
        return ( x + ( _gridRowMaxHalf ) ) + 
            ( ( y + ( _gridRowMaxHalf ) ) << _exponent);
    }
    
    private void SigToGrid( int sig, IntVector2 vec )
    {
        vec.x = ( sig % GridRowMax ) - ( _gridRowMaxHalf );
        sig >>= _exponent;
        vec.y = ( sig % GridRowMax ) - ( _gridRowMaxHalf );
    }

    private InternalOccupant CreateInternalOccupant( Vector3 pos, GameObject tracked, int magnitude )
    {
        InternalOccupant o = _occupantPool.GetObject();

        using (IntVector2 grid = _intVectorPool.GetObject())
        {
            PositionToGrid(pos, grid);

            o.Track(tracked);
            o.SetMagnitude(magnitude);
            o.Update(grid);
        }
        
        _occupants.Add(o);
        
        return o;
    }
    
    private void ReturnInternalOccupant( InternalOccupant occupant )
    {
        _occupants.Remove(occupant);
        
        occupant.DestroyVisuals();
        occupant.Occupy(true);
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
                    }
                }
            }
        }
    }

    private void FindNeighborClosestToPoint( Vector3 center, Vector3 dir, List<int> neighbors, IntVector2 closestNeighbor )
    {
        // Initialize the out vector just in case
        closestNeighbor.Set(0, 0);
    
        using( IntVector2 neighborVec = _intVectorPool.GetObject() )
        {
            float lastDot = -1;
            foreach (int neighborSig in neighbors)
            {

                SigToGrid(neighborSig, neighborVec);
                Vector3 neighborPos = Vector3.zero;
                GridToPosition(neighborVec.x, neighborVec.y, ref neighborPos);
            
                float dot = Vector3.Dot( dir, (center-neighborPos).normalized );
                if (dot > lastDot)
                {
                    closestNeighbor.Set(neighborVec);
                    lastDot = dot;
                }
            }
        }
    }

    private List< int > AcquireListOfUnoccupiedNeighbors( int currentSig )
    {
        List< int > outUnoccupiedNeighbors = _intListPool.GetObject();
        List< int > actedSigs = _intListPool.GetObject();
        Stack< int > neighborSigs = new Stack< int >();
        
        neighborSigs.Push(currentSig);
        
        // Search for a magnitude away from the occupant center that has empty spots
        while ( neighborSigs.Count > 0 )
        {
            int sig = neighborSigs.Pop();
            
            if( !IsOccupied( sig ) )
            {
                outUnoccupiedNeighbors.Add( sig );
            }
            
            actedSigs.Add( sig );
            
            if( neighborSigs.Count == 0 )
            {
                // If we already found some unoccupied neighbors, we can stop early
                if( outUnoccupiedNeighbors.Count == 0 )
                {
                    PushNewMagnitudeOfNeighborsIntoStack( actedSigs, neighborSigs );
                }
            }
        }

        _intListPool.ReturnObject(actedSigs);

        return outUnoccupiedNeighbors;
    }
    #endregion

    #if UNITY_EDITOR
    void FillValidGrids()
    {
        if (!Mathf.IsPowerOfTwo(GridRowMax))
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
            

            using (IntVector2 tmpGrid2 = _intVectorPool.GetObject())
            {
                while (neighborsToTry.Count > 0)
                {
                    maxStackSize = Mathf.Max(maxStackSize, neighborsToTry.Count);
                    
                    sig = neighborsToTry.Pop();
                    SigToGrid(sig, grid);

                    triedValues.Add(sig, true);
                    
                    GridToPosition(grid.x, grid.y, ref pos);
                    if (NavMesh.SamplePosition(pos, out hit, GridSize, -1))
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
    #endif

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
        private Stack< int > _neighbors = new Stack< int >();       // Stack for ring iteration
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
        public void Update( IntVector2 vec )
        {
            _current = _manager.GetGridSig(vec);
            
            // Stamp this occupant into the grid
            Occupy(false);
        }
        
        // Add or remove this occupants area to the grid
        public void Occupy( bool remove )
        {
            _debugTileCounter = 0; // More straighforward counter for which tile is being updated within UpdateDebugVisuals
            
            // List of grid sigs we have visited
            List< int > actedGrids = _manager._intListPool.GetObject();
            
            _neighbors.Push( _current );
            
            int magnitudeCounter = 0;
            while( _neighbors.Count > 0 )
            {
                int sig = _neighbors.Pop();
                
                if( remove ) 
                {
                    RemoveFootprintFromGrid( sig );
                }
                else 
                {
                    AddFootprintToGrid( sig );
                }
                
                actedGrids.Add( sig );
                
                UpdateDebugVisuals( remove, sig );
                
                // Once we've processed all the neighbors, go to the next ring if needed
                if( _neighbors.Count == 0 && magnitudeCounter < _magnitude )
                {
                    // Collect the next ring of neighbors into the stack
                    _manager.PushNewMagnitudeOfNeighborsIntoStack(actedGrids,_neighbors);
                    
                    magnitudeCounter++;
                }
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
                    _manager._intListPool.ReturnObject( bucket );
                    _manager._occupantBuckets.Remove( sig );
                }
            }
        }
        
        private void UpdateDebugVisuals( bool remove, int sig )
        {
            if( !remove && _manager.ShowDebug && _manager.OccupiedTilePrefab != null )
            {
                // Attempt to reuse a grid
                if( _debugTileCounter >= debugVisuals.Count )
                {
                    GameObject newVisual = Instantiate( _manager.OccupiedTilePrefab ) as GameObject;
                    newVisual.transform.parent = _manager.transform;
                    debugVisuals.Add( newVisual );
                }
                Vector3 pos = Vector3.zero;
                using( IntVector2 vec = _manager._intVectorPool.GetObject())
                {
                    _manager.SigToGrid( sig, vec );
                    _manager.GridToPosition( vec.x, vec.y, ref pos );
                    
                    debugVisuals[ _debugTileCounter ].transform.position = pos + (Vector3.up * 0.002f);

                    // Re-use pos
                    pos.Set( vec.x, 0, vec.y );
                    debugVisuals[ _debugTileCounter ].gameObject.SetActive( _manager.IsValid( pos ) );
                }
                
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
