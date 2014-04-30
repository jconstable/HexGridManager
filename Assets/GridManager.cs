using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Execute in edit mode to enable the rebuild checkbox
[ExecuteInEditMode]
public class GridManager : MonoBehaviour 
{
    // Simple container to remove need for a normal Vector2 for holding grid coordinates
    public struct IntVector2
    {
        public int x;
        public int y;

        public void Set( int _x, int _y ) { x = _x; y = _y; }

        public bool Equals( ref IntVector2 other )
        {
            return (x == other.x) && (y == other.y);
        }
    }

    // Public interface to allow users of this class to reference their Occupants without 
    // access to the implementation
    public interface Occupant {
    }

	public interface Reservation {
	}

    // Class that ecapsulates a 2D region of occupied grid squares
    private class InternalOccupant : Occupant, Reservation
    {
        // Static to uniquely identify every Occupant that is created
        private static int IdCounter = 0;

        private int _id = -1;                                       // Unique ID
        private GridManager _manager;                               // Reference to the parent grid manager
        private GridManager.IntVector2 _current;                    // Last known coordinates of this occupant
        private int _magnitude;                                     // The extent to which this Occupant extends from center
        private int _debugTileCounter = 0;

        // List to hold debug visual currently used by this Occupant
        private List< GameObject > debugVisuals = new List<GameObject>();

        // Getter for the occupant ID
        public int ID { get { return _id; } }

        public InternalOccupant( GridManager manager )
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
        public void Setup( IntVector2 vec )
        {
            _current = vec;

            // Stamp this occupant into the grid
            Occupy(false);
        }

        // Add or remove this occupants area to the grid
        public void Occupy( bool remove )
        {
            _debugTileCounter = 0; // More straighforward counter for which tile is being updated within UpdateDebugVisuals
            GridManager.IntVector2 temp = new GridManager.IntVector2();

            // For each row in this occupant's area
            for( int i = -_magnitude; i <= _magnitude; i++ )
            {
                // For each column in this occupant's area
                for( int j = -_magnitude; j <= _magnitude; j++ )
                {
                    temp.Set( _current.x + i, _current.y + j );
                    int sig = _manager.GetGridSig( ref temp );

                    if( remove ) 
                    {
                        RemoveFootprintFromGrid( sig );
                    }
                    else 
                    {
                        AddFootprintToGrid( sig );
                    }

                    UpdateDebugVisuals( remove, ref temp );
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

        private void UpdateDebugVisuals( bool remove, ref GridManager.IntVector2 vec )
        {
            if( !remove && _manager._showDebug && _manager.occupiedTilePrefab != null )
            {
                // Attempt to reuse a grid
                if( _debugTileCounter >= debugVisuals.Count )
                {
                    GameObject newVisual = Instantiate( _manager.occupiedTilePrefab ) as GameObject;
                    newVisual.transform.localScale = new Vector3( _manager.GridSize, _manager.GridSize, 1f );
                    debugVisuals.Add( newVisual );
                }
                debugVisuals[ _debugTileCounter ].transform.position = new Vector3( vec.x * _manager.GridSize, 0.002f, vec.y * _manager.GridSize );
                
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

    public GameObject vacantTilePrefab = null;
    public GameObject occupiedTilePrefab = null;

    public List<int> validGrids = new List< int >();
    protected Dictionary< int, List< int > > _occupantBuckets = new Dictionary<int, List< int > >();
    private List< InternalOccupant > _occupants = new List<InternalOccupant>();

    public bool _showDebug = false;
    private List< GameObject > _debugVisuals = new List<GameObject>();

    private GenericPool< List< int > > _intListPool = null;
    private GenericPool< InternalOccupant > _occupantPool = null;

	// Use this for initialization
    public GridManager () {
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

                    int xx = Mathf.Abs(_tmpGrid.x % 2);
                    int yy = Mathf.Abs(_tmpGrid.y % 2);
                    if ((xx == 0 && yy == 1) || (xx == 1 && yy == 0))
                    {
                        GameObject o = Instantiate(vacantTilePrefab) as GameObject;
                        o.transform.forward = Vector3.down;
                        o.transform.position = new Vector3(_tmpGrid.x * GridSize, 0.001f, _tmpGrid.y * GridSize);
                        o.transform.localScale = new Vector3(GridSize, GridSize, GridSize);
                        _debugVisuals.Add(o);
                    }
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
        return ( vec.x + ( GridRowMax / 2 ) ) + ( ( vec.y + ( GridRowMax / 2 ) ) << _exponent);
    }

    private void SigToGrid( int sig, ref IntVector2 vec )
    {
        vec.x = ( sig % GridRowMax ) - ( GridRowMax / 2 );
        sig >>= _exponent;
        vec.y = ( sig % GridRowMax ) - ( GridRowMax / 2 );
    }

    public void PositionToGrid( Vector3 pos, ref IntVector2 grid )
    {
        grid.x = (int)(pos.x / GridSize);
        grid.y = (int)(pos.z / GridSize);
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
        occupant.Setup(_tmpGrid);
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
        neighborsToTry.Push(GetGridSig(ref _tmpGrid));
        
        int sig = 0;
        int x, y;
        
        int maxStackSize = 0;
        IntVector2 tmpGrid2 = new IntVector2();

        while (neighborsToTry.Count > 0)
        {
            maxStackSize = Mathf.Max( maxStackSize, neighborsToTry.Count );
            
            sig = neighborsToTry.Pop();
            SigToGrid( sig, ref _tmpGrid );
            x = _tmpGrid.x;
            y = _tmpGrid.y;
            
            triedValues.Add( sig, true );
            
            pos.Set( x * GridSize, 0f, y * GridSize );
            if (NavMesh.SamplePosition(pos, out hit, GridSize, -1))
            {
                _tmpGrid.Set( x, y );
                validGrids.Add(GetGridSig(ref _tmpGrid));
                
                int nextX, nextY = 0;

                for (int i = 0; i < 4; i++)
                {
                    if (i == 0 || i == 2)
                    {
                        nextX = x + ((i == 0) ? 1 : -1);
                        nextY = y;
                    } else
                    {
                        nextY = y + ((i == 1) ? -1 : 1);
                        nextX = x;
                    }

                    tmpGrid2.Set( nextX, nextY );
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
