using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Execute in edit mode to enable the rebuild checkbox
[ExecuteInEditMode]
public class HexGridManager : MonoBehaviour 
{
    // Simple container to remove need for a normal Vector2 for holding grid coordinates
    public struct IntVector2
    {
        public int x;
        public int y;

        public IntVector2( ref IntVector2 other ) { x = other.x; y = other.y; }
        public IntVector2( int _x, int _y ) { x = _x; y = _y; }
        
        public void Set( int _x, int _y ) { x = _x; y = _y; }
        
        public bool Equals( ref IntVector2 other )
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
        private HexGridManager.IntVector2 _current;                 // Last known coordinates of this occupant
        private int _magnitude;                                     // The extent to which this Occupant extends from center
        private int _debugTileCounter = 0;
        
        // List to hold debug visual currently used by this Occupant
        private List< GameObject > debugVisuals = new List<GameObject>();
        
        // Getter for the occupant ID
        public int ID { get { return _id; } }
        public HexGridManager.IntVector2 Current { get { return _current; } }
        
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
            foreach (GameObject o in debugVisuals)
            {
                Destroy(o);
            }
            
            debugVisuals.Clear();
            _debugTileCounter = 0;
        }
        
        // Mark the grid with this occupant's new coordinates
        public void Setup( ref IntVector2 vec )
        {
            _current = vec;
            
            // Stamp this occupant into the grid
            Occupy(false);
        }
        
        // Add or remove this occupants area to the grid
        public void Occupy( bool remove )
        {
            _debugTileCounter = 0; // More straighforward counter for which tile is being updated within UpdateDebugVisuals

            List< HexGridManager.IntVector2 > actedGrids = new List<HexGridManager.IntVector2>();

            Stack< HexGridManager.IntVector2 > neighbors = new Stack< HexGridManager.IntVector2 >();
            neighbors.Push(new HexGridManager.IntVector2(ref _current));

            int magnitudeCounter = 0;
            while( neighbors.Count > 0 )
            {
                HexGridManager.IntVector2 coord = neighbors.Pop();
                int sig = _manager.GetGridSig( ref coord );

                if( remove ) 
                {
                    RemoveFootprintFromGrid( sig );
                }
                else 
                {
                    AddFootprintToGrid( sig );
                }

                actedGrids.Add( coord );
                
                UpdateDebugVisuals( remove, ref coord );

                if( neighbors.Count == 0 && magnitudeCounter < _magnitude )
                {
                    for( int k = 0; k < actedGrids.Count; k++ )
                    {
                        HexGridManager.IntVector2 acted = actedGrids[ k ];
                        for( int i = 0; i < HexGridManager.neighborVectors.Length; i++ )
                        {
                            HexGridManager.IntVector2 neighbor = 
                                new HexGridManager.IntVector2( acted.x + HexGridManager.neighborVectors[ i ].x,
                                                              acted.y + HexGridManager.neighborVectors[ i ].y );
                            if( _manager.IsValid( ref neighbor ) && !neighbors.Contains( neighbor ) )
                            {
                                neighbors.Push( neighbor );
                            }
                        }
                    }

                    magnitudeCounter++;
                }
            }
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
        
        private void UpdateDebugVisuals( bool remove, ref HexGridManager.IntVector2 vec )
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
                _manager.GridToPosition( ref vec, ref pos );
                debugVisuals[ _debugTileCounter ].transform.position = pos + (Vector3.up * 0.002f);
                
                _debugTileCounter++;
            }
        }
    }
    
    // Simple class to support templated object pooling
    private class GenericPool<T> {
        private Stack< T > _pool = new Stack< T >();
        
        public delegate T CreateObject();
        
        private CreateObject _func = null;
        
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
            
            return obj;
        }
        
        public void ReturnObject( T obj )
        {
            _pool.Push(obj);
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
    }
    
    List< int > CreateNewList()
    {
        return new List<int>();
    }
    
    InternalOccupant CreateNewOccupant()
    {
        return new InternalOccupant(this);
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
                    
                    SigToGrid(sig, ref _tmpGrid);

                    GameObject o = Instantiate(vacantTilePrefab) as GameObject;
                    Vector3 pos = Vector3.zero;
                    GridToPosition( ref _tmpGrid, ref pos );
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
    
    public bool IsValid( ref IntVector2 vec )
    {
        int sig = GetGridSig(ref vec);
        
        return (validGrids.BinarySearch(sig) > -1);
    }
    
    public bool IsOccupied( ref IntVector2 vec, Reservation optionalFilter = null )
    {
        List< int > occupants = null;
        bool exists = _occupantBuckets.TryGetValue(GetGridSig(ref vec), out occupants);
        
        if( optionalFilter == null || !exists )
        {
            return exists;
        }
        
        InternalOccupant occupant = optionalFilter as InternalOccupant;
        return !occupants.Contains(occupant.ID);
    }
    
    private int GetGridSig( ref IntVector2 vec )
    {
        return ( vec.x + ( _gridRowMaxHalf ) ) + 
            ( ( vec.y + ( _gridRowMaxHalf ) ) << _exponent);
    }
    
    private void SigToGrid( int sig, ref IntVector2 vec )
    {
        vec.x = ( sig % GridRowMax ) - ( _gridRowMaxHalf );
        sig >>= _exponent;
        vec.y = ( sig % GridRowMax ) - ( _gridRowMaxHalf );
    }
    
    public void PositionToGrid( Vector3 pos, ref IntVector2 grid )
    {
        float g = (float)GridSize;
        float z = pos.z / (g * (3.0f / 2.0f));

        float t = g * Mathf.Sqrt(3.0f);
        float x = ( pos.x / t ) - ( z / 2.0f );
        grid.x = (int)x;
        grid.y = (int)z;
    }
    
    public void GridToPosition( ref IntVector2 grid, ref Vector3 pos )
    {
        float x = (float)grid.x;
        float y = (float)grid.y;
        float g = (float)GridSize;

        pos.x = g * Mathf.Sqrt(3.0f) * (x + ( y / 2.0f ) );
        pos.y = 0f;
        pos.z = g * (3.0f / 2.0f) * y;
    }

    public IntVector2 GetClosestVacantNeighbor( GameObject dest, GameObject src = null )
    {
        IntVector2 occupantCurrent = new IntVector2();
        PositionToGrid(dest.transform.position, ref occupantCurrent);

        if (!IsOccupied(ref occupantCurrent))
        {
            return occupantCurrent;
        }

        List< int > actedSigs = new List< int >();
        Stack< IntVector2 > neighbors = new Stack< IntVector2 >();
        neighbors.Push(occupantCurrent);

        List< IntVector2 > unoccupiedNeighbors = new List< IntVector2 >();

        // Search for a magnitude away from the occupant center that has empty spots
        while ( neighbors.Count > 0 )
        {
            IntVector2 coord = neighbors.Pop();
            int sig = GetGridSig( ref coord );
            if( !actedSigs.Contains( sig ) )
            {
                actedSigs.Add( sig );
            }

            if( !IsOccupied( ref coord ) )
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
                        SigToGrid( neighborSig, ref acted );

                        for( int i = 0; i < neighborVectors.Length; i++ )
                        {
                            IntVector2 neighbor = 
                                new IntVector2( acted.x + neighborVectors[ i ].x,
                                                acted.y + neighborVectors[ i ].y );
                            if( IsValid( ref neighbor ) && !neighbors.Contains( neighbor ) && !actedSigs.Contains( GetGridSig( ref neighbor ) ) )
                            {
                                neighbors.Push( neighbor );
                            }
                        }
                    }
                }
            }
        }

        IntVector2 nearest = new IntVector2();
        if (unoccupiedNeighbors.Count > 0)
        {
            // Now that we have a list of unoccupied neighbors, find the one closest to who is asking
            if (src == null)
            {
                nearest = unoccupiedNeighbors [0];
            } else {
                IntVector2 askingObjectGrid = new IntVector2();
                PositionToGrid( src.transform.position, ref askingObjectGrid );


                float lastMag = float.MaxValue;
                foreach( IntVector2 neighbor in unoccupiedNeighbors )
                {
                    IntVector2 neighborVec = neighbor;
                    Vector3 neighborPos = Vector3.zero;
                    GridToPosition( ref neighborVec, ref neighborPos );
                    float diffMag = ( neighborPos - src.transform.position).sqrMagnitude;
                    if( diffMag < lastMag )
                    {
                        nearest = neighbor;
                        lastMag = diffMag;
                    }
                }
            }
        }

        return nearest;
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
        
        PositionToGrid(thing.transform.position, ref _tmpGrid);
        
        occupant.Occupy(true);
        occupant.Setup( ref _tmpGrid );
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
        
        PositionToGrid(pos, ref _tmpGrid);
        
        o.SetMagnitude(magnitude);
        o.Setup(ref _tmpGrid);
        
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
        neighborsToTry.Push(GetGridSig(ref _tmpGrid));
        
        int sig = 0;
        
        int maxStackSize = 0;
        IntVector2 tmpGrid2 = new IntVector2();
        
        while (neighborsToTry.Count > 0)
        {
            maxStackSize = Mathf.Max( maxStackSize, neighborsToTry.Count );
            
            sig = neighborsToTry.Pop();
            SigToGrid( sig, ref _tmpGrid );

            triedValues.Add( sig, true );
            
            GridToPosition( ref _tmpGrid, ref pos );
            if (NavMesh.SamplePosition(pos, out hit, GridSize, -1))
            {
                validGrids.Add(sig);

                for (int i = 0; i < HexGridManager.neighborVectors.Length; i++)
                {
                    IntVector2 neighborDir = HexGridManager.neighborVectors[ i ];
                    
                    tmpGrid2.Set( neighborDir.x + _tmpGrid.x, neighborDir.y + _tmpGrid.y );
                    int nextSig = GetGridSig(ref tmpGrid2);
                    if (!triedValues.TryGetValue(nextSig, out b) && !neighborsToTry.Contains(nextSig))
                    {
                        neighborsToTry.Push(nextSig);
                    }
                }
            }
        }
        
        // Sort so we can bsearch it
        validGrids.Sort();
        
        Debug.Log("Found valid squares: " + validGrids.Count);
        Debug.Log("Max stack size during search: " + maxStackSize);
    }
    #endif
}
