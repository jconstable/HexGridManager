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
    
    public interface Reservation {
    }
    
    // Class that ecapsulates a 2D region of occupied grid squares
    protected class InternalOccupant : Occupant, Reservation
    {
        // Static to uniquely identify every Occupant that is created
        private static int IdCounter = 0;
        
        private int _id = -1;                                       // Unique ID
        private HexGridManager _manager;                            // Reference to the parent grid manager
        private IntVector2 _current;                                // Last known coordinates of this occupant
        private int _magnitude;                                     // The extent to which this Occupant extends from center
        private int _debugTileCounter = 0;
        
        // List to hold debug visual currently used by this Occupant
        private List< GameObject > debugVisuals = new List<GameObject>();
        
        // Getter for the occupant ID
        public int ID { get { return _id; } }
        public HexGridManager.IntVector2 Current { get { return _current; } }
        
        public InternalOccupant( HexGridManager manager )
        {
            _current = manager._intVectorPool.GetObject();
            _manager = manager;
            _id = IdCounter++;
        }

        ~InternalOccupant()
        {
            _manager._intVectorPool.ReturnObject( _current );
        }
        
        // Set the magnitude for the occupant
        public void SetMagnitude( int magnitude )
        {
            _magnitude = magnitude;
            
            DestroyVisuals();
        }
        
        public void DestroyVisuals()
        {
            foreach (GameObject o in debugVisuals)
            {
                Destroy(o);
            }
            
            debugVisuals.Clear();
            _debugTileCounter = 0;
        }
        
        // Mark the grid with this occupant's new coordinates
        public void Setup( IntVector2 vec )
        {
            _current.x = vec.x;
            _current.y = vec.y;
            
            // Stamp this occupant into the grid
            Occupy(false);
        }
        
        // Add or remove this occupants area to the grid
        public void Occupy( bool remove )
        {
            _debugTileCounter = 0; // More straighforward counter for which tile is being updated within UpdateDebugVisuals

            List< IntVector2 > actedGrids = new List<IntVector2>();

            Stack< IntVector2 > neighbors = new Stack< IntVector2 >();

            IntVector2 first = _manager._intVectorPool.GetObject();
            first.Set(_current);
            neighbors.Push(first);

            int magnitudeCounter = 0;
            while( neighbors.Count > 0 )
            {
                IntVector2 coord = neighbors.Pop();
                int sig = _manager.GetGridSig( coord );

                if( remove ) 
                {
                    RemoveFootprintFromGrid( sig );
                }
                else 
                {
                    AddFootprintToGrid( sig );
                }

                actedGrids.Add( coord );
                
                UpdateDebugVisuals( remove, coord );

                if( neighbors.Count == 0 && magnitudeCounter < _magnitude )
                {
                    for( int k = 0; k < actedGrids.Count; k++ )
                    {
                        IntVector2 acted = actedGrids[ k ];
                        for( int i = 0; i < neighborVectors.Length; i++ )
                        {
                            IntVector2 neighbor = _manager._intVectorPool.GetObject();
                            neighbor.Set( acted.x + neighborVectors[ i ].x,
                                          acted.y + neighborVectors[ i ].y );
                            if( _manager.IsValid( neighbor ) && !neighbors.Contains( neighbor ) )
                            {
                                neighbors.Push( neighbor );
                            }
                        }
                    }

                    magnitudeCounter++;
                }
            }

            foreach (IntVector2 v in actedGrids)
            {
                _manager._intVectorPool.ReturnObject( v );
            }
            actedGrids.Clear();

            _manager._intVectorPool.PrintStats();
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
        
        private void UpdateDebugVisuals( bool remove, IntVector2 vec )
        {
            if( !remove && _manager._showDebug && _manager.occupiedTilePrefab != null )
            {
                // Attempt to reuse a grid
                if( _debugTileCounter >= debugVisuals.Count )
                {
                    GameObject newVisual = Instantiate( _manager.occupiedTilePrefab ) as GameObject;
                    debugVisuals.Add( newVisual );
                }
                Vector3 pos = Vector3.zero;
                _manager.GridToPosition( vec, ref pos );
                debugVisuals[ _debugTileCounter ].transform.position = pos + (Vector3.up * 0.002f);
                
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
            _pool.Clear();
        }
        
        public T GetObject()
        {
            T obj = default(T);
            
            if (_pool.Count > 0)
            {
                obj = _pool.Pop();
            } else
            {
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
            Debug.Log("GenericPool stats: " + _pool.Count + " in queue, " + _outstanding + " outstanding");
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
        if (Application.isPlaying)
        {
            if (_showDebug && vacantTilePrefab != null && _debugVisuals.Count == 0)
            {
                foreach (int sig in validGrids)
                {
                    
                    SigToGrid(sig, _tmpGrid);

                    GameObject o = Instantiate(vacantTilePrefab) as GameObject;
                    Vector3 pos = Vector3.zero;
                    GridToPosition( _tmpGrid, ref pos );
                    o.transform.position = pos + ( Vector3.up * 0.001f );
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
        List< int > occupants = null;
        bool exists = _occupantBuckets.TryGetValue(GetGridSig(vec), out occupants);
        
        if( optionalFilter == null || !exists )
        {
            return exists;
        }
        
        InternalOccupant occupant = optionalFilter as InternalOccupant;
        return !occupants.Contains(occupant.ID);
    }
    
    private int GetGridSig( IntVector2 vec )
    {
        return ( vec.x + ( _gridRowMaxHalf ) ) + 
            ( ( vec.y + ( _gridRowMaxHalf ) ) << _exponent);
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

    public void GetClosestVacantNeighbor( GameObject dest, ref IntVector2 closestNeighbor, GameObject src = null )
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
        Stack< IntVector2 > neighbors = new Stack< IntVector2 >();
        neighbors.Push(occupantCurrent);

        List< IntVector2 > unoccupiedNeighbors = new List< IntVector2 >();

        // Search for a magnitude away from the occupant center that has empty spots
        while ( neighbors.Count > 0 )
        {
            IntVector2 coord = neighbors.Pop();
            int sig = GetGridSig( coord );
            if( !actedSigs.Contains( sig ) )
            {
                actedSigs.Add( sig );
            }

            if( !IsOccupied( coord ) )
            {
                unoccupiedNeighbors.Add( coord );
            }

            if( neighbors.Count == 0 )
            {
                if( unoccupiedNeighbors.Count == 0 )
                {
                    for( int k = 0; k < actedSigs.Count; k++ )
                    {
                        int neighborSig = actedSigs[k];
                        IntVector2 acted = new IntVector2();
                        SigToGrid( neighborSig, acted );

                        for( int i = 0; i < neighborVectors.Length; i++ )
                        {
                            IntVector2 neighbor = 
                                new IntVector2( acted.x + neighborVectors[ i ].x,
                                                acted.y + neighborVectors[ i ].y );
                            if( IsValid( neighbor ) && !neighbors.Contains( neighbor ) && !actedSigs.Contains( GetGridSig( neighbor ) ) )
                            {
                                neighbors.Push( neighbor );
                            }
                        }
                    }
                }
            }
        }

        IntVector2 nearest = _intVectorPool.GetObject();
        if (unoccupiedNeighbors.Count > 0)
        {
            // Now that we have a list of unoccupied neighbors, find the one closest to who is asking
            if (src == null)
            {
                nearest = unoccupiedNeighbors [0];
            } else {
                IntVector2 askingObjectGrid = new IntVector2();
                PositionToGrid( src.transform.position, askingObjectGrid );


                float lastMag = float.MaxValue;
                foreach( IntVector2 neighbor in unoccupiedNeighbors )
                {
                    IntVector2 neighborVec = neighbor;
                    Vector3 neighborPos = Vector3.zero;
                    GridToPosition( neighborVec, ref neighborPos );
                    float diffMag = ( neighborPos - src.transform.position).sqrMagnitude;
                    if( diffMag < lastMag )
                    {
                        nearest = neighbor;
                        lastMag = diffMag;
                    }
                }
            }
        }

        closestNeighbor.Set( nearest );
        _intVectorPool.ReturnObject( nearest );
    }
    
    #region Occupants
    private IntVector2 _tmpGrid = new IntVector2();
    public Occupant CreateOccupant( GameObject thing, int magnitude )
    {
        return (Occupant)CreateInternalOccupant( thing.transform.position, magnitude );
    }
    
    public void UpdateOccupant( Occupant occ, GameObject thing )
    {
        InternalOccupant occupant = (InternalOccupant)occ;
        
        PositionToGrid(thing.transform.position, _tmpGrid);
        
        occupant.Occupy(true);
        occupant.Setup( _tmpGrid );
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
        return (Reservation)CreateInternalOccupant( pos, 0 );
    }
    
    public void ReturnReservation( ref Reservation res )
    {
        ReturnInternalOccupant (res as InternalOccupant);
        res = null;
    }
    #endregion
    
    private InternalOccupant CreateInternalOccupant( Vector3 pos, int magnitude )
    {
        InternalOccupant o = _occupantPool.GetObject();
        
        PositionToGrid(pos, _tmpGrid);
        
        o.SetMagnitude(magnitude);
        o.Setup(_tmpGrid);
        
        _occupants.Add(o);
        
        return o;
    }
    
    private void ReturnInternalOccupant( InternalOccupant occupant )
    {
        _occupants.Remove(occupant);
        
        occupant.DestroyVisuals();
        occupant.Occupy(true);
        
        _occupantPool.ReturnObject(occupant);
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
        _tmpGrid.Set(0, 0);
        neighborsToTry.Push(GetGridSig(_tmpGrid));
        
        int sig = 0;
        
        int maxStackSize = 0;
        IntVector2 tmpGrid2 = new IntVector2();
        
        while (neighborsToTry.Count > 0)
        {
            maxStackSize = Mathf.Max( maxStackSize, neighborsToTry.Count );
            
            sig = neighborsToTry.Pop();
            SigToGrid( sig, _tmpGrid );

            triedValues.Add( sig, true );
            
            GridToPosition( _tmpGrid, ref pos );
            if (NavMesh.SamplePosition(pos, out hit, GridSize, -1))
            {
                validGrids.Add(sig);

                for (int i = 0; i < HexGridManager.neighborVectors.Length; i++)
                {
                    IntVector2 neighborDir = HexGridManager.neighborVectors[ i ];
                    
                    tmpGrid2.Set( neighborDir.x + _tmpGrid.x, neighborDir.y + _tmpGrid.y );
                    int nextSig = GetGridSig(tmpGrid2);
                    if (!triedValues.TryGetValue(nextSig, out b) && !neighborsToTry.Contains(nextSig))
                    {
                        neighborsToTry.Push(nextSig);
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
        
        Debug.Log("Found valid squares: " + validGrids.Count);
        Debug.Log("Max stack size during search: " + maxStackSize);
    }
    #endif
}
