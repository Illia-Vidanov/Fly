using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public class MarchingCubes : MonoBehaviour
{
  [Header("General")]
  [SerializeField] private float cube_size_;
  [SerializeField] private Vector3Int cube_dimensions_;
  [SerializeField] private int fraction_decimals_power_;

  [Header("Values")]
  [SerializeField] private float value_amplitude_;
  [SerializeField] private Vector3 value_frequency_;

  [Header("Mesh")]
  [SerializeField] private MeshFilter mesh_filter_;
  [SerializeField] private float solid_thrashhold_;

  private Mesh mesh_;

  [Header("Input")]
  [SerializeField] private bool regenerate_;

  [Header("Debug")]
  private List<Tuple<Vector3, Vector3>> debug_lines_ = new List<Tuple<Vector3, Vector3>>();

  private System.Random rng_;

  private List<List<int>> cubes_;
  private List<Vector3> verts_;
  private List<float> values_;


  void Start()
  {
    rng_ = new System.Random();
    Generate();
  }

  void Update()
  {
    HandleInput();
    DebugDrawLines(debug_lines_.ToArray());
  }

  private void HandleInput()
  {
    if(regenerate_)
    {
      regenerate_ = false;
      Generate();
    }
  }

  private void Generate()
  {
    GenerateVerts();
    GenerateCubes();
    GenerateValues();
    GenerateMesh();
    /*
    for(int cube_i = 0; cube_i < cubes_.Count; ++cube_i)
    {
      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][0]], verts_[cubes_[cube_i][1]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][1]], verts_[cubes_[cube_i][2]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][2]], verts_[cubes_[cube_i][3]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][3]], verts_[cubes_[cube_i][0]]));

      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][4]], verts_[cubes_[cube_i][5]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][5]], verts_[cubes_[cube_i][6]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][6]], verts_[cubes_[cube_i][7]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][7]], verts_[cubes_[cube_i][4]]));

      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][0]], verts_[cubes_[cube_i][4]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][1]], verts_[cubes_[cube_i][5]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][2]], verts_[cubes_[cube_i][6]]));
      debug_lines_.Add(new Tuple<Vector3, Vector3>(verts_[cubes_[cube_i][3]], verts_[cubes_[cube_i][7]]));
    } */
  }

  private void GenerateVerts()
  {
    verts_ = new List<Vector3>(cube_dimensions_.x * cube_dimensions_.y * cube_dimensions_.z);

    for(int x = 0; x < cube_dimensions_.x; ++x)
    {
      for(int y = 0; y < cube_dimensions_.y; ++y)
      {
        for(int z = 0; z < cube_dimensions_.z; ++z)
        {
          verts_.Add(new Vector3(x * cube_size_, y * cube_size_, z * cube_size_));
        }
      }
    }
  }

  private void GenerateCubes()
  {
    cubes_ = new List<List<int>>((cube_dimensions_.x - 1) * (cube_dimensions_.y - 1) * (cube_dimensions_.z - 1));
    for(int x = 0; x < cube_dimensions_.x - 1; ++x)
    {
      for(int y = 0; y < cube_dimensions_.y - 1; ++y)
      {
        for(int z = 0; z < cube_dimensions_.z - 1; ++z)
        {
          cubes_.Add(new List<int>(new[] {Coordinate3DToIndex(x, y, z), Coordinate3DToIndex(x + 1, y, z), Coordinate3DToIndex(x + 1, y + 1, z), 
                                          Coordinate3DToIndex(x, y + 1, z), Coordinate3DToIndex(x, y, z + 1), Coordinate3DToIndex(x + 1, y, z + 1), 
                                          Coordinate3DToIndex(x + 1, y + 1, z + 1), Coordinate3DToIndex(x, y + 1, z + 1)}));
        }
      }
    }
  }

  private int Coordinate3DToIndex(int x, int y, int z)
  {
    return x * cube_dimensions_.y * cube_dimensions_.z + y * cube_dimensions_.z + z;
  }

  private void GenerateValues()
  {
    float[] values = new float[verts_.Count];

    NoiseDotNet.NoiseSettings noise_settings = new NoiseDotNet.NoiseSettings();
    noise_settings.Amplitude = value_amplitude_;
    noise_settings.XFrequency = value_frequency_.x;
    noise_settings.YFrequency = value_frequency_.y;
    noise_settings.ZFrequency = value_frequency_.z;
    noise_settings.Seed = rng_.Next();

    NoiseDotNet.Noise.GradientNoise3D(new ReadOnlySpan<float>(verts_.Select(vector => vector.x).ToArray()),
                                      new ReadOnlySpan<float>(verts_.Select(vector => vector.y).ToArray()),
                                      new ReadOnlySpan<float>(verts_.Select(vector => vector.z).ToArray()),
                                      values, noise_settings);

    values_ = new List<float>(values);
  }

  private void GenerateMesh()
  {
    mesh_ = new Mesh();

    List<Vector3> verts = new List<Vector3>();
    List<int> inds = new List<int>();
    Dictionary<Vector3Int, int> known_verts = new Dictionary<Vector3Int, int>();

    for(int cube_i = 0; cube_i < cubes_.Count; ++cube_i)
    {
      int iso_value = CalculateIsoValue(cubes_[cube_i]);
      for(int inds_i = 0; MarchingCubesLookup.kTriangles[iso_value][inds_i] != - 1; ++inds_i)
      {
        Vector3 edge_middle = Vector3.Lerp(verts_[cubes_[cube_i][MarchingCubesLookup.kEdgeToVertex[MarchingCubesLookup.kTriangles[iso_value][inds_i]][0]]],
                                           verts_[cubes_[cube_i][MarchingCubesLookup.kEdgeToVertex[MarchingCubesLookup.kTriangles[iso_value][inds_i]][1]]],
                                           0.5f);
        
        int known_index;
        if(known_verts.TryGetValue(FloatToIntVector(edge_middle), out known_index))
        {
          inds.Add(known_index);
          continue;
        }

        verts.Add(edge_middle);
        known_verts.Add(FloatToIntVector(edge_middle), verts.Count - 1);
        inds.Add(verts.Count - 1);
      }
    }

    mesh_.SetVertices(verts);
    mesh_.SetIndices(inds, MeshTopology.Triangles, 0);
    mesh_.RecalculateNormals();
    mesh_filter_.mesh = mesh_;
  }

  private Vector3 IntToFloatVector(Vector3Int vector)
  {
    return new Vector3((float)vector.x / fraction_decimals_power_, (float)vector.y / fraction_decimals_power_, (float)vector.z / fraction_decimals_power_);
  }

  private Vector3Int FloatToIntVector(Vector3 vector)
  {
    return new Vector3Int(Mathf.RoundToInt(vector.x * fraction_decimals_power_), Mathf.RoundToInt(vector.y * fraction_decimals_power_), Mathf.RoundToInt(vector.z * fraction_decimals_power_));
  }

  private int CalculateIsoValue(List<int> inds)
  {
    Debug.Assert(inds.Count == 8);
    int result = 0;
    for(int i = 0; i < 8; ++i)
    {
      if(values_[inds[i]] > solid_thrashhold_)
        continue;
      
      result |= MarchingCubesLookup.kLocalIndexToBit[i];
    }

    return result;
  }

  private void DebugDrawLines(Tuple<Vector3, Vector3>[] lines)
  {
    for(int i = 0; i < lines.Length; ++i)
    {
      Debug.DrawLine(lines[i].Item1, lines[i].Item2, Color.red);  
    }
  }
}