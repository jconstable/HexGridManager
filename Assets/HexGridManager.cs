using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Execute in edit mode to enable the rebuild checkbox
[ExecuteInEditMode]
public class HexGridManager : MonoBehaviour 
{
    // Simple container to remove need for a normal Vector2 for holding grid coordinates
    public class IntVector2
    {
        public int x;
        public int y;

        public IntVector2() { x = 0; y = 0; }
        public IntVector2( IntVector2 other ) { x = other.x; y = other.y; }
        public IntVector2( int _x, int _y ) { x = _x; y = _y; }

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
            if( !remove && _manager._showDebug && _manager.occupiedTilePrefab != null )
            {
                // Attempt to reuse a grid
                if( _debugTileCounter >= debugVisuals.Count )
                {
                    GameObject newVisual = Instantiate( _manager.occupiedTilePrefab ) as GameObject;
                    newVisual.transform.parent = _manager.transform;
                    debugVisuals.Add( newVisual );
                }
                Vector3 pos = Vector3.zero;
                IntVector2 vec = _manager._intVectorPool.GetObject();
                _manager.SigToGrid( sig, vec );
                _manager.GridToPosition( vec, ref pos );

                debugVisuals[ _debugTileCounter ].transform.position = pos + (Vector3.up * 0.002f);
                debugVisuals[ _debugTileCounter ].gameObject.SetActive( _manager.IsValid( vec ) );

                _manager._intVectorPool.ReturnObject( vec );
                
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
        public GenericPool( CreateObject func )
        {
            _func = func;
        }
        
        ~GenericPool()
        {
            if( _outstanding > 0 )
            {
                Debug.LogError("GenericPool being destroyed before all items were returned");
            }
            _pool.Clear();
        }
        
        public T GetObject()
        {
            T obj = default(T);
            
            if (_pool.Count > 0) {
                obj = _pool.Pop();
            } else {
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
            Debug.Log("GenericPool stats: (" + typeof(T).ToString() + "): " + _pool.Count + " in queue, " + _outstanding + " outstanding");
        }
    };
    
    // Inspector checkbox to trigger rebuilding of tiles
    public bool Rebuild = false;
    
    // The size in Unity meters of the length of one grid square
    public float GridSize = 0.25f;
    // The length in squares of one side of the entire possible grid space
    public int GridRowMax = 1024;
    
    private int _exponent = 0;
    private int _gridRowMaxHalf = 0;
    
    public GameObject vacantTilePrefab = null;
    public GameObject occupiedTilePrefab = null;
    
    public List<int> validGrids = new List< int >();
    protected Dictionary< int, List< int > > _occupantBuckets = new Dictionary<int, List< int > >();
    private List< InternalOccupant > _occupants = new List<InternalOccupant>();
    
    public bool _showDebug = false;
    private List< GameObject > _debugVisuals = new List<GameObject>();
    
    private GenericPool< List< int > > _intListPool = null;
    private GenericPool< InternalOccupant > _occupantPool = null;
    protected GenericPool< IntVector2 > _intVectorPool = null;

    public static readonly IntVector2[] neighborVectors = new IntVector2[6] {
        new IntVector2( +1,  0 ),
        new IntVector2( +1, -1 ), 
        new IntVector2( 0, -1 ),
        new IntVector2( -1,  0 ),
        new IntVector2( -1, +1 ), 
        new IntVector2( 0, +1 )
    };
    
    // Use this for initialization
    public HexGridManager () {
        _intListPool = new GenericPool< List< int > >(CreateNewList);
        _occupantPool = new GenericPool< InternalOccupant >(CreateNewOccupant);
        _intVectorPool = new GenericPool<IntVector2>(CreateNewIntVector);
    }
    
    List< int > CreateNewList()
    {
        return new List<int>();
    }
    
    InternalOccupant CreateNewOccupant()
    {
        return new InternalOccupant(this);
    }

    IntVector2 CreateNewIntVector()
    {
        return new IntVector2();
    }
    
    public void Awake()
    {
        DetermineExponent();
    }
    
    private void DetermineExponent()
    {
        while (( GridRowMax >> _exponent ) != 1)
        {
            _exponent++;
        }

        _gridRowMaxHalf = GridRowMax / 2;
    }
    
    // Update is called once per frame
    void Update () 
    {
        // Process occupants and signal if they have moved to a new grid
        IntVector2 grid = _intVectorPool.GetObject();
        foreach (InternalOccupant occupant in _occupants)
        {
            if( occupant.TrackedGameObject != null )
            {
                PositionToGrid(occupant.TrackedGameObject.transform.position, grid);
                int sig = GetGridSig( grid );
            
                // See if it's moved 
                if( sig != occupant.Sig )
                {
                    occupant.Occupy(true);
                    occupant.Update( grid );
            
                    occupant.TrackedGameObject.SendMessage( "OnGridChanged" );
                }
            }
        }

        if (Application.isPlaying)
        {
            if (_showDebug && vacantTilePrefab != null && _debugVisuals.Count == 0)
            {
                foreach (int sig in validGrids)
                {
                    
                    SigToGrid(sig, grid);

                    GameObject o = Instantiate(vacantTilePrefab) as GameObject;
                    Vector3 pos = Vector3.zero;
                    GridToPosition( grid, ref pos );
                    o.transform.position = pos + ( Vector3.up * 0.001f );
                    o.transform.parent = transform;
                    _debugVisuals.Add(o);
                }
                
            }
            
            if (!_showDebug && _debugVisuals.Count > 0)
            {
                DestroyVisuals();
                foreach (InternalOccupant occ in _occupants)
                {
                    occ.DestroyVisuals();
                }
            }
        }
        #if UNITY_EDITOR
        else
        {
            if (Rebuild)
            {
                validGrids.Clear();
                Rebuild = false;
                System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
                FillValidGrids();
                stopwatch.Stop();
                Debug.Log("GridManager: Setup took " + stopwatch.ElapsedMilliseconds + "ms" );
            }
        }
        #else
        Rebuild = false;
        #endif

        _intVectorPool.ReturnObject(grid);
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
    
    public bool IsValid( IntVector2 vec )
    {
        int sig = GetGridSig(vec);
        
        return (validGrids.BinarySearch(sig) > -1);
    }

    public bool IsOccupied( IntVector2 vec, Reservation optionalFilter = null )
    {
        return IsOccupied(GetGridSig(vec), optionalFilter);
    }
    
    public bool IsOccupied( int sig, Reservation optionalFilter = null )
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
    
    public void PositionToGrid( Vector3 pos, IntVector2 grid )
    {
        float g = (float)GridSize;

        /*
        float z = pos.z / (g * (3.0f / 2.0f));

        float t = g * Mathf.Sqrt(3.0f);
        float x = ( pos.x ) - ( (t * z) / 2.0f );
        x /= t;
        grid.x = (int)x;
        grid.y = (int)z;*/


        float q = ((1f/3f) * ( Mathf.Sqrt(3f) *  pos.x )) - ((1f/3f) * pos.z );
        q /= g;
        float r = ((2f / 3f) * pos.z) / g;

        /*
        float x = (pos.x - (g/2f)) / g;
        
        float t1 = pos.z / (g / 2f);
        float t2 = Mathf.Floor(x + t1);
        float r = Mathf.Floor((Mathf.Floor(t1 - x) + t2) / 3f); 
        float q = Mathf.Floor((Mathf.Floor( 2f * x + 1f) + t2) / 3f) - r;*/

        grid.x = Mathf.RoundToInt( q );
        grid.y = Mathf.RoundToInt( r );
    }
    
    public void GridToPosition( IntVector2 grid, ref Vector3 pos )
    {
        float x = (float)grid.x;
        float y = (float)grid.y;
        float g = (float)GridSize;

        pos.x = g * Mathf.Sqrt(3.0f) * (x + ( y / 2.0f ) );
        pos.y = 0f;
        pos.z = g * (3.0f / 2.0f) * y;
    }

    public void GetClosestVacantNeighbor( GameObject dest, IntVector2 closestNeighbor, GameObject src = null )
    {
        IntVector2 occupantCurrent = _intVectorPool.GetObject();
        PositionToGrid(dest.transform.position, occupantCurrent);

        if (!IsOccupied(occupantCurrent))
        {
            closestNeighbor.Set( occupantCurrent );
            _intVectorPool.ReturnObject( occupantCurrent );
            return;
        }

        List< int > actedSigs = new List< int >();
        Stack< int > neighborSigs = new Stack< int >();

        neighborSigs.Push(GetGridSig(occupantCurrent));
        _intVectorPool.ReturnObject( occupantCurrent );

        List< int > unoccupiedNeighbors = new List< int >();

        // Search for a magnitude away from the occupant center that has empty spots
        while ( neighborSigs.Count > 0 )
        {
            int sig = neighborSigs.Pop();

            if( !IsOccupied( sig ) )
            {
                unoccupiedNeighbors.Add( sig );
            }

            actedSigs.Add( sig );

            if( neighborSigs.Count == 0 )
            {
                if( unoccupiedNeighbors.Count == 0 )
                {
                    PushNewMagnitudeOfNeighborsIntoStack( actedSigs, neighborSigs );
                }
            }
        }

        IntVector2 nearest = _intVectorPool.GetObject();
        if (unoccupiedNeighbors.Count > 0)
        {
            // Now that we have a list of unoccupied neighbors, find the one closest to who is asking
            if (src == null)
            {
                SigToGrid( unoccupiedNeighbors[0], nearest );
            } else {
                IntVector2 askingObjectGrid = _intVectorPool.GetObject();
                PositionToGrid( src.transform.position, askingObjectGrid );

                float lastMag = float.MaxValue;
                foreach( int neighborSig in unoccupiedNeighbors )
                {
                    IntVector2 neighborVec = _intVectorPool.GetObject();
                    SigToGrid( neighborSig, neighborVec);
                    Vector3 neighborPos = Vector3.zero;
                    GridToPosition( neighborVec, ref neighborPos );

                    float diffMag = ( neighborPos - src.transform.position).sqrMagnitude;
                    if( diffMag < lastMag )
                    {
                        nearest.Set( neighborVec );
                        lastMag = diffMag;
                    }

                    _intVectorPool.ReturnObject( neighborVec );
                }

                _intVectorPool.ReturnObject( askingObjectGrid );
            }
        }

        closestNeighbor.Set( nearest );
        _intVectorPool.ReturnObject( nearest );
    }
    
    #region Occupants
    public Occupant CreateOccupant( GameObject thing, int magnitude )
    {
        return (Occupant)CreateInternalOccupant( thing.transform.position, thing, magnitude );
    }
    
    public void ReturnOccupant( ref Occupant occ )
    {
        ReturnInternalOccupant (occ as InternalOccupant);
        occ = null;
    }
    #endregion
    
    #region Reservations
    public Reservation CreateReservation( Vector3 pos )
    {
        return (Reservation)CreateInternalOccupant( pos, null, 0 );
    }
    
    public void ReturnReservation( ref Reservation res )
    {
        ReturnInternalOccupant (res as InternalOccupant);
        res = null;
    }
    #endregion
    
    private InternalOccupant CreateInternalOccupant( Vector3 pos, GameObject tracked, int magnitude )
    {
        InternalOccupant o = _occupantPool.GetObject();

        IntVector2 grid = _intVectorPool.GetObject();
        PositionToGrid(pos, grid);

        o.Track(tracked);
        o.SetMagnitude(magnitude);
        o.Update(grid);

        _intVectorPool.ReturnObject(grid);
        
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
        IntVector2 actedVec = _intVectorPool.GetObject();
        IntVector2 neighborVec = _intVectorPool.GetObject();
        
        foreach( int acted in actedGrids )
        {
            for( int i = 0; i < neighborVectors.Length; i++ )
            {
                SigToGrid( acted, actedVec );
                
                neighborVec.Set( actedVec.x + neighborVectors[ i ].x,
                                actedVec.y + neighborVectors[ i ].y );
                
                int neighborSig = GetGridSig( neighborVec );
                if( !neighbors.Contains( neighborSig ) && !actedGrids.Contains( neighborSig ) )
                {
                    neighbors.Push( neighborSig );
                }
            }
        }
        
        _intVectorPool.ReturnObject( actedVec );
        _intVectorPool.ReturnObject( neighborVec );
    }
    
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
        
        NavMeshHit hit;
        Vector3 pos = new Vector3();
        
        Stack< int > neighborsToTry = new Stack< int >();
        
        // Start it off
        IntVector2 grid = _intVectorPool.GetObject();
        grid.Set(0, 0);
        neighborsToTry.Push(GetGridSig(grid));
        
        int sig = 0;
        
        int maxStackSize = 0;
        IntVector2 tmpGrid2 = new IntVector2();
        
        while (neighborsToTry.Count > 0)
        {
            maxStackSize = Mathf.Max( maxStackSize, neighborsToTry.Count );
            
            sig = neighborsToTry.Pop();
            SigToGrid( sig, grid );

            triedValues.Add( sig, true );
            
            GridToPosition( grid, ref pos );
            if (NavMesh.SamplePosition(pos, out hit, GridSize, -1))
            {
                validGrids.Add(sig);

                for (int i = 0; i < HexGridManager.neighborVectors.Length; i++)
                {
                    IntVector2 neighborDir = HexGridManager.neighborVectors[ i ];
                    
                    tmpGrid2.Set( neighborDir.x + grid.x, neighborDir.y + grid.y );
                    int nextSig = GetGridSig(tmpGrid2);
                    if (!triedValues.TryGetValue(nextSig, out b) && !neighborsToTry.Contains(nextSig))
                    {
                        neighborsToTry.Push(nextSig);
                    }
                }
            }
        }

        _intVectorPool.ReturnObject(grid);
        
        // Sort so we can bsearch it
        validGrids.Sort();

        if (validGrids.Count == 0)
        {
            Debug.LogError("Unable to find any possible grid units in this scene. Make sure you have baked a nav mesh, and that the nav mesh includes the world origin (0,0)");
            return;
        }
        
        Debug.Log("Found valid squares: " + validGrids.Count);
        Debug.Log("Max stack size during search: " + maxStackSize);
    }
    #endif
}
