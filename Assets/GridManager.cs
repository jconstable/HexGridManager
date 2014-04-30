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
    }

    // Public interface to allow users of this class to reference their Occupants without 
    // access to the implementation
    public interface Occupant {
    }

    // Class that ecapsulates a 2D region of occupied grid squares
    private class InternalOccupant : Occupant 
    {
        // Static to uniquely identify every Occupant that is created
        private static int IdCounter = 0;

        private int _id = -1;                                       // Unique ID
        private GridManager _manager;                               // Reference to the parent grid manager
        private int _currentX;                                      // Last known center of this Occupant :X
        private int _currentY;                                      //                                    :Y
        private int _magnitude;                                     // The extent to which this Occupant extends from center

        // List to hold debug visual currently used by this Occupant
        private List< GameObject > debugVisuals = new List<GameObject>();

        public InternalOccupant( GridManager manager )
        {
            _manager = manager;
            _id = IdCounter++;
        }

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
        }

        public void Setup( int x, int y )
        {
            _currentX = x;
            _currentY = y;

            // Stamp this occupant into the grid
            Occupy(false);
        }

        // Add or remove this occupants area to the grid
        public void Occupy( bool remove )
        {
            int debugTileCounter = 0;

            // For each row in this occupant's area
            for( int i = -_magnitude; i <= _magnitude; i++ )
            {
                // For each column in this occupant's area
                for( int j = -_magnitude; j <= _magnitude; j++ )
                {
                    int x = _currentX + i;
                    int y = _currentY + j;
                    int sig = _manager.GetGridSig( x, y );
                    List< int > bucket = null;
                    if( _manager._occupantBuckets.TryGetValue( sig, out bucket ) )
                    {
                        if( remove )
                        {
                            bucket.Remove( _id );

                            if( bucket.Count == 0 )
                            {
                                _manager._intListPool.ReturnObject( bucket );
                                _manager._occupantBuckets.Remove( sig );
                            }
                        } else {
                            if( !bucket.Contains( _id ) )
                            {
                                bucket.Add( _id );
                            }
                        }
                    } else {
                        if( !remove )
                        {
                            bucket = _manager._intListPool.GetObject();
                            bucket.Add( _id );
                            _manager._occupantBuckets.Add( sig, bucket );
                        }
                    }

                    if( !remove && _manager._showDebug && _manager.occupiedTilePrefab != null )
                    {
                        // Attempt to reuse a grid
                        if( debugTileCounter >= debugVisuals.Count )
                        {
                            GameObject newVisual = Instantiate( _manager.occupiedTilePrefab ) as GameObject;
                            newVisual.transform.localScale = new Vector3( _manager.GridSize, _manager.GridSize, 1f );
                            debugVisuals.Add( newVisual );
                        }
                        debugVisuals[ debugTileCounter ].transform.position = new Vector3( x * _manager.GridSize, 0.002f, y * _manager.GridSize );

                        debugTileCounter++;
                    }
                }
            }
        }
    }

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

    public bool Rebuild = false;

	public float GridSize = 0.25f;
	public int GridRowMax = 1024;

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

    // Update is called once per frame
	void Update () 
    {
        if (Application.isPlaying)
        {
            if (_showDebug && vacantTilePrefab != null && _debugVisuals.Count == 0)
            {
                foreach (int sig in validGrids)
                {
                    int x, y;
                    SigToGrid(sig, out x, out y);

                    int xx = Mathf.Abs(x % 2);
                    int yy = Mathf.Abs(y % 2);
                    if ((xx == 0 && yy == 1) || (xx == 1 && yy == 0))
                    {
                        GameObject o = Instantiate(vacantTilePrefab) as GameObject;
                        o.transform.forward = Vector3.down;
                        o.transform.position = new Vector3(x * GridSize, 0.001f, y * GridSize);
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

    public bool IsValid( int x, int y )
    {
        int sig = GetGridSig(x, y);

        return (BinarySearch(validGrids, sig) > -1);
    }

    public bool IsOccupied( int x, int y )
    {
        List< int > occupants = null;
        return _occupantBuckets.TryGetValue(GetGridSig(x, y), out occupants);
    }

    private int GetGridSig( int x, int y )
    {
        /*
         * int packedX = x + (GridRowMax / 2);
        int packedY = y + (GridRowMax / 2);
        return ( packedX ) + ( packedY * GridRowMax ); */

        return ( x + (GridRowMax / 2) ) + ( ( y + (GridRowMax / 2) ) * GridRowMax );
    }

    private void SigToGrid( int sig, out int x, out int y )
    {
        x = ( sig % GridRowMax ) - ( GridRowMax / 2 );

        int packedY = sig - (x + (GridRowMax / 2));
        packedY /= GridRowMax;
        y = packedY - ( GridRowMax / 2 ); 
    }

    public void PositionToGrid( Vector3 pos, ref IntVector2 grid )
    {
        grid.x = (int)(pos.x / GridSize);
        grid.y = (int)(pos.z / GridSize);
    }

    private IntVector2 _tmpGrid = new IntVector2();
    public Occupant CreateOccupant( GameObject thing, int magnitude )
    {
        InternalOccupant o = _occupantPool.GetObject();

        PositionToGrid(thing.transform.position, ref _tmpGrid);

        o.SetMagnitude(magnitude);
        o.Setup(_tmpGrid.x, _tmpGrid.y);

        _occupants.Add(o);

        return (Occupant)o;
    }

    public void UpdateOccupant( Occupant occ, GameObject thing )
    {
        InternalOccupant occupant = (InternalOccupant)occ;

        PositionToGrid(thing.transform.position, ref _tmpGrid);

        occupant.Occupy(true);
        occupant.Setup(_tmpGrid.x, _tmpGrid.y);
    }

    public void ReturnOccupant( ref Occupant occ )
    {
        InternalOccupant occupant = occ as InternalOccupant;
        _occupants.Remove(occupant);

        occupant.DestroyVisuals();
        occupant.Occupy(true);

        _occupantPool.ReturnObject(occupant);
        occ = null;
    }

    private int BinarySearch( List< int > list, int value )
    {
        if (list == null)
            return -1;

        int floor = 0;
        int ceil = Mathf.Max( 0, list.Count - 1 );
        int mid = -1;
        int midIndex = -1;

        while (floor < ceil)
        {
            if( list[ floor ] == value )return floor;
            if( list[ ceil ] == value )return ceil;

            if( floor + 1 == ceil )
            {
                return -1;
            }

            midIndex = ( ( ceil - floor ) / 2 ) + floor;
            mid = list[ midIndex ];
            
            if( mid == value )
            { 
                return midIndex; 
            }
            
            if( mid > value ) 
            { 
                ceil = midIndex;
            }
            else
            {
                floor = midIndex;
            }
        }
        
        
        return -1;
    }

#if UNITY_EDITOR
    void FillValidGrids()
    {
        Dictionary< int, bool > triedValues = new Dictionary< int, bool >();
        bool b;
        
        NavMeshHit hit;
        Vector3 pos = new Vector3();
        
        Stack< int > neighborsToTry = new Stack< int >();
        
        // Start it off
        neighborsToTry.Push(GetGridSig(0, 0));
        
        int sig = 0;
        int x, y;
        
        int maxStackSize = 0;
        
        while (neighborsToTry.Count > 0)
        {
            maxStackSize = Mathf.Max( maxStackSize, neighborsToTry.Count );
            
            sig = neighborsToTry.Pop();
            SigToGrid( sig, out x, out y );
            
            triedValues.Add( sig, true );
            
            pos.Set( x * GridSize, 0f, y * GridSize );
            if (NavMesh.SamplePosition(pos, out hit, GridSize, -1))
            {
                validGrids.Add(GetGridSig(x, y));
                
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
                    
                    int nextSig = GetGridSig(nextX, nextY);
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
