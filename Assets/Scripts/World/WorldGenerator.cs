using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

public class WorldGenerator : MonoBehaviour
{
  [Header("General")]
  [SerializeField] private int hexagon_size_;
  [SerializeField] private float hexagon_point_distance_;
  [SerializeField] private Transform start_position_;
  [SerializeField] private int fraction_decimals_;
  [SerializeField] private int smoothing_iterations_;
  [SerializeField] [Range(0.0f, 1.0f)] private float smoothing_factor_;

  [Header("Layers")]
  [SerializeField] private int layers_;
  [SerializeField] private float layer_distance_;
  [SerializeField] private Vector2 layer_frequency_;
  [SerializeField] private float layer_amplitude_;

  [Header("Values")]
  [SerializeField] private Vector3 value_frequency_;
  [SerializeField] private float value_amplitude_;
  
  [Header("Mesh")]
  [SerializeField] private float mesh_solid_trashhold_;
  
  [Header("Controls")]
  [SerializeField] private bool regenerate_;

  [Header("Debug")]
  [SerializeField] private GameObject debug_point_;
  [SerializeField] private MeshFilter mesh_filter_;
  [SerializeField] private Material default_material_;

  private HashSet<Tuple<Vector3, Vector3>> debug_lines_ = new HashSet<Tuple<Vector3, Vector3>>(new LineComparer());
  
  // privates

  private System.Random rng_;

  // Those variables are transitional (will be destructed after generation)
  private Vector3[] directions_;
  private Vector3[] steps_;
  private float min_edge_vertex_distance_sqr_;
  private float max_edge_vertex_distance_sqr_;
  private float fraction_decimals_power_;
  private List<Vector3> verts_ = new List<Vector3>();
  // For future me. I use HashSet here to not to deal with collisions. But it might be worth performance-wise to use list idk.
  private Dictionary<Vector3Int, HashSet<Vector3Int>> large_vertex_neighbors_ = new Dictionary<Vector3Int, HashSet<Vector3Int>>();
  private HashSet<Tuple<Vector3Int, Vector3Int>> lines_ = new HashSet<Tuple<Vector3Int, Vector3Int>>(new LineComparerInt());
  private List<Tuple<Vector3Int, Vector3Int>> removed_lines_ = new List<Tuple<Vector3Int, Vector3Int>>();
  private Dictionary<Vector3Int, HashSet<Vector3Int>> small_vertex_neighbors_ = new Dictionary<Vector3Int, HashSet<Vector3Int>>();
  private List<List<Vector3Int>> quads_ = new List<List<Vector3Int>>();
  
  // Those are resulting lists from world generation
  private List<Vector3> vertex_position_;
  private List<List<int>> vertex_neighbors_;
  private List<float> vertex_value_;
  private List<List<int>> cubes_;

  // constants

  private const int kHexagonSides = 6;
  private Vector3Int kNoCommonNeighboor = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);

  private class LineComparerInt : IEqualityComparer<Tuple<Vector3Int, Vector3Int>>
  {
    bool IEqualityComparer<Tuple<Vector3Int, Vector3Int>>.Equals(Tuple<Vector3Int, Vector3Int> x, Tuple<Vector3Int, Vector3Int> y)
    {
      return (x.Item1 == y.Item1 && x.Item2 == y.Item2) || (x.Item1 == y.Item2 && x.Item2 == y.Item1);
    }

    int IEqualityComparer<Tuple<Vector3Int, Vector3Int>>.GetHashCode(Tuple<Vector3Int, Vector3Int> obj)
    {
      return obj.Item1.GetHashCode() + obj.Item2.GetHashCode();
    }
  };

  private class LineComparer : IEqualityComparer<Tuple<Vector3, Vector3>>
  {
    bool IEqualityComparer<Tuple<Vector3, Vector3>>.Equals(Tuple<Vector3, Vector3> x, Tuple<Vector3, Vector3> y)
    {
      return (x.Item1 == y.Item1 && x.Item2 == y.Item2) || (x.Item1 == y.Item2 && x.Item2 == y.Item1);
    }

    int IEqualityComparer<Tuple<Vector3, Vector3>>.GetHashCode(Tuple<Vector3, Vector3> obj)
    {
      return obj.Item1.GetHashCode() + obj.Item2.GetHashCode();
    }
  };

  private class Vector3IntComparer : IComparer<Vector3Int>
  {
    int IComparer<Vector3Int>.Compare(Vector3Int lhs, Vector3Int rhs)
    {
      if(lhs == rhs)
        return 0;

      if(lhs.x < rhs.x)
        return -1;
      else if(lhs.x == rhs.x)
      {
        if(lhs.y < rhs.y)
          return -1;
        else if(lhs.y == rhs.y)
        {
          if(lhs.z < rhs.z)
            return -1;
        }
      }
      return 1;
    }
  }

  private class ListDummy<T>
  {
    public T data;
  }


  private void Start()
  {
    //RunTests();
    rng_ = new System.Random();

    GenerateGrid();
  }

  private void Update()
  {
    DebugDrawLines(debug_lines_.ToArray());
    HandleControls();
  }

  private void HandleControls()
  {
    if(regenerate_)
    {
      debug_lines_.Clear();
      GenerateGrid();
      regenerate_ = false;
    }
  }

  private void GenerateGrid()
  {
    if(hexagon_size_ < 2)
    {
      Debug.LogError("hexagon_size_ is too small");
      return;
    }

    if(layers_ < 1)
    {
      Debug.LogError("There is less than 1 layer");
      return;
    }

    double start_time = Time.realtimeSinceStartup;
    fraction_decimals_power_ = Mathf.Pow(10, fraction_decimals_);

    float long_distance = Mathf.Sin(Mathf.Deg2Rad * 60) * hexagon_point_distance_;
    float short_distance = Mathf.Sin(Mathf.Deg2Rad * 30) * hexagon_point_distance_;

    directions_ = new Vector3[]{new Vector3(0, 0, -hexagon_point_distance_),
                                new Vector3(-long_distance, 0, -short_distance),
                                new Vector3(-long_distance, 0, short_distance),
                                new Vector3(0, 0, hexagon_point_distance_),
                                new Vector3(long_distance, 0, short_distance),
                                new Vector3(long_distance, 0, -short_distance)};
    
    steps_ = new Vector3[]{directions_[1] - directions_[0],
                           directions_[2] - directions_[1],
                           directions_[3] - directions_[2],
                           directions_[4] - directions_[3],
                           directions_[5] - directions_[4],
                           directions_[0] - directions_[5]};

    min_edge_vertex_distance_sqr_ = Mathf.Pow(long_distance * (hexagon_size_ - 1) - 0.01f, 2);
    max_edge_vertex_distance_sqr_ = Mathf.Pow(long_distance * hexagon_size_ - 0.01f, 2);

    GenerateVerts();

    GenerateLines();

    GenerateLargeVertexNeighbors();

    RemoveRandomLinesToMakeQuads();

    GenerateSmallVertexNeighborsAlongLines();

    FindAndPoluteTriangles();

    PoluteQuads();

    ConstructResultLists();

    CleanUp();

    ApplySmoothing();

    AddLayers();

    GenerateValues();
    /*
    for(int cube_i = 0; cube_i < cubes_.Count; ++cube_i)
    {
      Debug.Assert(cubes_[cube_i].Count == 8);
      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][0]], vertex_position_[cubes_[cube_i][1]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][1]], vertex_position_[cubes_[cube_i][2]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][2]], vertex_position_[cubes_[cube_i][3]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][3]], vertex_position_[cubes_[cube_i][0]]));

      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][4]], vertex_position_[cubes_[cube_i][5]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][5]], vertex_position_[cubes_[cube_i][6]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][6]], vertex_position_[cubes_[cube_i][7]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][7]], vertex_position_[cubes_[cube_i][4]]));

      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][0]], vertex_position_[cubes_[cube_i][4]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][1]], vertex_position_[cubes_[cube_i][5]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][2]], vertex_position_[cubes_[cube_i][6]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(vertex_position_[cubes_[cube_i][3]], vertex_position_[cubes_[cube_i][7]]));
    } */

    for(int i = 0; i < vertex_position_.Count; ++i)
    {
      GameObject point = Instantiate(debug_point_, vertex_position_[i], Quaternion.identity);
      point.name = vertex_value_[i].ToString();
      Material material = new Material(default_material_);
      material.color = Color.HSVToRGB(0.0f, 0.0f, Mathf.Clamp(vertex_value_[i] / value_amplitude_, -1.0f, 1.0f));
      point.GetComponent<MeshRenderer>().material = material;
    }

    Debug.Log("Generation time: " + (Time.realtimeSinceStartup - start_time).ToString());
  }

  private void GenerateVerts()
  {
    Vector3 space_start;

    // We use local coordinates at first, but at the end we will add start position to every vertex to shift it
    verts_.Add(Vector3.zero);
    for(int layer_i = 1; layer_i < hexagon_size_; ++layer_i)
    {
      for(int side_i = 0; side_i < kHexagonSides; ++side_i)
      {
        verts_.Add(directions_[side_i] * layer_i);
        space_start = verts_[verts_.Count - 1];
        for(int space_i = 1; space_i < layer_i; ++space_i)
          verts_.Add(space_start + (steps_[side_i] * space_i));
      }
    }
  }

  private void GenerateLargeVertexNeighbors()
  {
    foreach (Tuple<Vector3Int, Vector3Int> line in lines_)
    {
      if(!large_vertex_neighbors_.ContainsKey(line.Item1))
        large_vertex_neighbors_[line.Item1] = new HashSet<Vector3Int>();
      if(!large_vertex_neighbors_.ContainsKey(line.Item2))
        large_vertex_neighbors_[line.Item2] = new HashSet<Vector3Int>();
      large_vertex_neighbors_[line.Item1].Add(line.Item2);
      large_vertex_neighbors_[line.Item2].Add(line.Item1);
    }
  }

  private void GenerateLines()
  {
    for(int vert_i = 0; vert_i < verts_.Count; ++vert_i)
    {
      for(int side_i = 0; side_i < kHexagonSides; ++side_i)
      {
        if((verts_[vert_i] + directions_[side_i]).sqrMagnitude >= max_edge_vertex_distance_sqr_)
          continue;
        lines_.Add(new Tuple<Vector3Int, Vector3Int>(FloatToIntVector(verts_[vert_i]), FloatToIntVector(verts_[vert_i] + directions_[side_i])));
      }
    }
  }

  private void RemoveRandomLinesToMakeQuads()
  {
    Tuple<Vector3Int, Vector3Int>[] shuffled_lines_ = lines_.ToArray();
    Utilities.ShuffleArray(shuffled_lines_, rng_);

    for(int i = 0; i < shuffled_lines_.Length; ++i)
    {
      // I am not sure if I can make IsEdgeVertex accept int representation and whether I should do that
      if(IsEdgeVertex(IntToFloatVector(shuffled_lines_[i].Item1)) && IsEdgeVertex(IntToFloatVector(shuffled_lines_[i].Item2)))
        continue;
      if(!DoPointsHave2CommonNeighbors(shuffled_lines_[i].Item1, shuffled_lines_[i].Item2))
        continue;
      lines_.Remove(shuffled_lines_[i]);
      large_vertex_neighbors_[shuffled_lines_[i].Item1].Remove(shuffled_lines_[i].Item2);
      large_vertex_neighbors_[shuffled_lines_[i].Item2].Remove(shuffled_lines_[i].Item1);
      removed_lines_.Add(shuffled_lines_[i]);
    }
  }

  private void GenerateSmallVertexNeighborsAlongLines()
  {
    foreach (Tuple<Vector3Int, Vector3Int> line in lines_)
    {
      // Math should work in this line without converting it to floats, but I am not sure about whether or not we should truncate it or round.
      // It will be trouncated here and because it's new point anyway this shouldn't be a problem
      Vector3Int middle = (line.Item1 + line.Item2) / 2;
      if(!small_vertex_neighbors_.ContainsKey(line.Item1))
        small_vertex_neighbors_[line.Item1] = new HashSet<Vector3Int>();
      if(!small_vertex_neighbors_.ContainsKey(middle))
        small_vertex_neighbors_[middle] = new HashSet<Vector3Int>();
      if(!small_vertex_neighbors_.ContainsKey(line.Item2))
        small_vertex_neighbors_[line.Item2] = new HashSet<Vector3Int>();
      small_vertex_neighbors_[middle].Add(line.Item1);
      small_vertex_neighbors_[middle].Add(line.Item2);
      small_vertex_neighbors_[line.Item1].Add(middle);
      small_vertex_neighbors_[line.Item2].Add(middle);
    }
  }

  private void FindAndPoluteTriangles()
  {
    foreach(Tuple<Vector3Int, Vector3Int> line in lines_)
    {
      Vector3Int common_neighbor = FindCommonNeighbor(line.Item1, line.Item2);
      if(common_neighbor == kNoCommonNeighboor)
        continue;

      Vector3Int triangle_middle = (line.Item1 + line.Item2 + common_neighbor) / 3;
      if(small_vertex_neighbors_.ContainsKey(triangle_middle))
        continue;
      Vector3Int line_middle_1 = (line.Item1 + line.Item2) / 2;
      Vector3Int line_middle_2 = (line.Item1 + common_neighbor) / 2;
      Vector3Int line_middle_3 = (line.Item2 + common_neighbor) / 2;
      small_vertex_neighbors_[triangle_middle] = new HashSet<Vector3Int>(new[]{line_middle_1, line_middle_2, line_middle_3});
      small_vertex_neighbors_[line_middle_1].Add(triangle_middle);
      small_vertex_neighbors_[line_middle_2].Add(triangle_middle);
      small_vertex_neighbors_[line_middle_3].Add(triangle_middle);

      // Add quads
      quads_.Add(new List<Vector3Int>(new[]{line.Item1, line_middle_1, triangle_middle, line_middle_2}));
      quads_.Add(new List<Vector3Int>(new[]{line.Item2, line_middle_1, triangle_middle, line_middle_3}));
      quads_.Add(new List<Vector3Int>(new[]{common_neighbor, line_middle_2, triangle_middle, line_middle_3}));
    }
  }

  private void PoluteQuads()
  {
    foreach(Tuple<Vector3Int, Vector3Int> line in removed_lines_)
    {
      Tuple<Vector3Int, Vector3Int> common_neighbors = Find2CommonNeighbors(line.Item1, line.Item2);
      // They should be present. If not something went wrong
      Debug.Assert(common_neighbors.Item1 != kNoCommonNeighboor && common_neighbors.Item2 != kNoCommonNeighboor);
      Vector3Int quad_middle = (line.Item1 + line.Item2 + common_neighbors.Item1 + common_neighbors.Item2) / 4;
      if(small_vertex_neighbors_.ContainsKey(quad_middle))
        continue;
      Vector3Int line_middle_1 = (line.Item1 + common_neighbors.Item1) / 2;
      Vector3Int line_middle_2 = (line.Item1 + common_neighbors.Item2) / 2;
      Vector3Int line_middle_3 = (line.Item2 + common_neighbors.Item1) / 2;
      Vector3Int line_middle_4 = (line.Item2 + common_neighbors.Item2) / 2;
      small_vertex_neighbors_[quad_middle] = new HashSet<Vector3Int>(new[]{line_middle_1, line_middle_2, line_middle_3, line_middle_4});
      small_vertex_neighbors_[line_middle_1].Add(quad_middle);
      small_vertex_neighbors_[line_middle_2].Add(quad_middle);
      small_vertex_neighbors_[line_middle_3].Add(quad_middle);
      small_vertex_neighbors_[line_middle_4].Add(quad_middle);

      // Add quads
      quads_.Add(new List<Vector3Int>(new[]{line.Item1, line_middle_1, quad_middle, line_middle_2}));
      quads_.Add(new List<Vector3Int>(new[]{line.Item2, line_middle_3, quad_middle, line_middle_4}));
      quads_.Add(new List<Vector3Int>(new[]{common_neighbors.Item1, line_middle_1, quad_middle, line_middle_3}));
      quads_.Add(new List<Vector3Int>(new[]{common_neighbors.Item2, line_middle_2, quad_middle, line_middle_4}));
    }
  }

  private void ConstructResultLists()
  {
    // We use separate array to avoid constant int to float vector conversions
    Vector3Int[] keys = small_vertex_neighbors_.Keys.ToArray();
    // We sort it to be able to do binary search
    Array.Sort(keys, new Vector3IntComparer());

    // Here we add our offset of start position
    vertex_position_ = new List<Vector3>(keys.Select(key => { return IntToFloatVector(key) + start_position_.position; }));
    vertex_neighbors_ = new List<List<int>>(vertex_position_.Count);
    cubes_ = new List<List<int>>(quads_.Count);

    for(int i = 0; i < keys.Length; ++i)
    {
      HashSet<Vector3Int> neighbors = small_vertex_neighbors_[keys[i]];
      vertex_neighbors_.Add(new List<int>(neighbors.Count));
      foreach(Vector3Int neighbor in neighbors)
        vertex_neighbors_[i].Add(Array.BinarySearch(keys, neighbor, new Vector3IntComparer()));
    }

    for(int i = 0; i < quads_.Count; ++i)
    {
      cubes_.Add(new List<int>(8));
      cubes_[i].AddRange(quads_[i].Select(position => Array.BinarySearch(keys, position, new Vector3IntComparer())));
    }
  }

  private void CleanUp()
  {
    large_vertex_neighbors_.Clear();
    small_vertex_neighbors_.Clear();
    verts_.Clear();
    lines_.Clear();
    removed_lines_.Clear();
    quads_.Clear();
  }

  private void ApplySmoothing()
  {
    for(int iteration_i = 0; iteration_i < smoothing_iterations_; ++iteration_i)
    {
      List<Vector3> old_position_ = vertex_position_;
      for(int vertex_i = 0; vertex_i < old_position_.Count; ++vertex_i)
      {
        if(IsEdgeVertex(old_position_[vertex_i]))
          continue;
        
        Vector3 desired_position = Vector3.zero;
        for(int neighbor_i = 0; neighbor_i < vertex_neighbors_[vertex_i].Count; ++neighbor_i)
          desired_position += old_position_[vertex_neighbors_[vertex_i][neighbor_i]];
        desired_position /= vertex_neighbors_[vertex_i].Count;
        vertex_position_[vertex_i] += (desired_position - vertex_position_[vertex_i]) * smoothing_factor_;
      }
    }
  }

  private void AddLayers()
  {
    if(layers_ < 2)
      return;

    int init_position_count = vertex_position_.Count;
    // It actually should be the same as position count
    int init_neighbors_count = vertex_neighbors_.Count;
    int init_cube_count = cubes_.Count;

    for(int i = 1; i < layers_; ++i)
    {
      vertex_position_.AddRange(vertex_position_.GetRange(0, init_position_count));
      vertex_neighbors_.AddRange(vertex_neighbors_.GetRange(0, init_neighbors_count).Select(list => new List<int>(list)));
      
      if(i == 1)
        continue;
        
      cubes_.AddRange(cubes_.GetRange(0, init_cube_count).Select(list => new List<int>(list)));
    }

    for(int vertex_i = init_neighbors_count; vertex_i < vertex_neighbors_.Count; ++vertex_i)
    {
      int layer = vertex_i / init_neighbors_count;
      int neighbor_count = vertex_neighbors_[vertex_i - init_neighbors_count * layer].Count;
      for(int neighbor_i = 0; neighbor_i < neighbor_count; ++neighbor_i)
        vertex_neighbors_[vertex_i][neighbor_i] += init_neighbors_count * layer;
      
      vertex_position_[vertex_i] += Vector3.up * (layer_distance_ * layer);
      
      if(layer != (layers_ - 1)) // if not top layer
        vertex_neighbors_[vertex_i].Add(vertex_i + init_neighbors_count);
      
      vertex_neighbors_[vertex_i].Add(vertex_i - init_neighbors_count);
    }

    // Add noise

    NoiseDotNet.NoiseSettings noise_settings = new NoiseDotNet.NoiseSettings();
    noise_settings.XFrequency = layer_frequency_.x;
    noise_settings.YFrequency = layer_frequency_.y;
    noise_settings.Amplitude = layer_amplitude_;
    noise_settings.Seed = rng_.Next();

    for(int layer_i = 0; layer_i < layers_; ++layer_i)
    {
      ++noise_settings.Seed;
      List<Vector3> slice = vertex_position_.GetRange(layer_i * init_position_count, init_position_count);
      float[] output = new float[init_position_count];
      NoiseDotNet.Noise.GradientNoise2D(new ReadOnlySpan<float>(slice.Select(vector => vector.x).ToArray()),
                                        new ReadOnlySpan<float>(slice.Select(vector => vector.z).ToArray()),
                                        output, noise_settings);

      for(int vertex_i = 0; vertex_i < init_position_count; ++vertex_i)
        vertex_position_[vertex_i + layer_i * init_position_count] += Vector3.up * output[vertex_i];
    }

    // Convert quads into cubes
    for(int cube_i = 0; cube_i < cubes_.Count; ++cube_i)
    {
      int layer = cube_i / init_cube_count;
      Debug.Assert(cubes_[cube_i].Count == 4);
      for(int vertex_i = 0; vertex_i < 4; ++vertex_i)
      {
        cubes_[cube_i][vertex_i] += init_neighbors_count * layer;
        cubes_[cube_i].Add(cubes_[cube_i][vertex_i] + init_neighbors_count);
      }
    }

    // We do it after loop, because in the loop we use values of initial layer
    for(int i = 0; i < init_neighbors_count; ++i)
      vertex_neighbors_[i].Add(i + init_neighbors_count);
  }

  private void GenerateValues()
  {
    NoiseDotNet.NoiseSettings noise_settings = new NoiseDotNet.NoiseSettings();
    noise_settings.XFrequency = value_frequency_.x;
    noise_settings.YFrequency = value_frequency_.y;
    noise_settings.ZFrequency = value_frequency_.y;
    noise_settings.Amplitude = value_amplitude_;
    noise_settings.Seed = rng_.Next();

    float[] output = new float[vertex_position_.Count];

    // Here I use a little bit of unsafe code to create values in place
    NoiseDotNet.Noise.GradientNoise3D(new ReadOnlySpan<float>(vertex_position_.Select(vector => vector.x).ToArray()),
                                      new ReadOnlySpan<float>(vertex_position_.Select(vector => vector.y).ToArray()),
                                      new ReadOnlySpan<float>(vertex_position_.Select(vector => vector.z).ToArray()),
                                      output, noise_settings);

    vertex_value_ = new List<float>(output);
  }

  // So here go 3 functions that can be replaced by a single one List<Vector3Int> FindCommonNeighbors(...), but I am not sure I want it
  // It works as it is

  private bool DoPointsHave2CommonNeighbors(Vector3Int point1, Vector3Int point2)
  {
    int matches = 0;
    foreach(Vector3Int point in large_vertex_neighbors_[point1])
    {
      if(large_vertex_neighbors_[point2].Contains(point))
        ++matches;
    }

    return matches == 2;
  }

  private Vector3Int FindCommonNeighbor(Vector3Int point1, Vector3Int point2)
  {
    foreach(Vector3Int point in large_vertex_neighbors_[point1])
    {
      if(large_vertex_neighbors_[point2].Contains(point))
        return point;
    }

    return kNoCommonNeighboor;
  }

  private Tuple<Vector3Int, Vector3Int> Find2CommonNeighbors(Vector3Int point1, Vector3Int point2)
  {
    Vector3Int first_neighbor = kNoCommonNeighboor;
    foreach(Vector3Int point in large_vertex_neighbors_[point1])
    {
      if(large_vertex_neighbors_[point2].Contains(point))
      {
        if(first_neighbor != kNoCommonNeighboor)
          return new Tuple<Vector3Int, Vector3Int>(first_neighbor, point);
        first_neighbor = point;
      }
    }

    return new Tuple<Vector3Int, Vector3Int>(first_neighbor, kNoCommonNeighboor);
  }

  private bool IsEdgeVertex(Vector3 point)
  {
    return point.sqrMagnitude >= min_edge_vertex_distance_sqr_;
  }

  private Vector3 IntToFloatVector(Vector3Int vector)
  {
    return new Vector3((float)vector.x / fraction_decimals_power_, (float)vector.y / fraction_decimals_power_, (float)vector.z / fraction_decimals_power_);
  }

  private Vector3Int FloatToIntVector(Vector3 vector)
  {
    return new Vector3Int(Mathf.RoundToInt(vector.x * fraction_decimals_power_), Mathf.RoundToInt(vector.y * fraction_decimals_power_), Mathf.RoundToInt(vector.z * fraction_decimals_power_));
  }

  private void RunTests()
  {
    IEqualityComparer<Tuple<Vector3Int, Vector3Int>> comparer = new LineComparerInt();
    Debug.Log("LineComparer.Equals test expected true, got: " + comparer.Equals(new Tuple<Vector3Int, Vector3Int>(Vector3Int.one, Vector3Int.zero), new Tuple<Vector3Int, Vector3Int>(Vector3Int.zero, Vector3Int.one)));
    Debug.Log("LineComparer.GetHashCoode test expected true, got: " + (comparer.GetHashCode(new Tuple<Vector3Int, Vector3Int>(Vector3Int.one, Vector3Int.zero)) == comparer.GetHashCode(new Tuple<Vector3Int, Vector3Int>(Vector3Int.zero, Vector3Int.one))).ToString());
    Debug.Log("LineComparer.Equals test expected true, got: " + comparer.Equals(new Tuple<Vector3Int, Vector3Int>(new Vector3Int(-87, 0, 50), Vector3Int.zero), new Tuple<Vector3Int, Vector3Int>(Vector3Int.zero, new Vector3Int(-87, 0, 50))));
    Debug.Log("LineComparer.GetHashCoode test expected true, got: " + (comparer.GetHashCode(new Tuple<Vector3Int, Vector3Int>(new Vector3Int(-87, 0, 50), Vector3Int.zero)) == comparer.GetHashCode(new Tuple<Vector3Int, Vector3Int>(Vector3Int.zero, new Vector3Int(-87, 0, 50)))).ToString());
  }

  private void DebugDrawLines(Tuple<Vector3, Vector3>[] lines)
  {
    for(int i = 0; i < lines.Length; ++i)
    {
      Debug.DrawLine(lines[i].Item1, lines[i].Item2, Color.red);  
    }
  }
}
