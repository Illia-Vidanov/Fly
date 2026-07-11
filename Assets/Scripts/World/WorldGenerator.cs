using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

public class WorldGenerator : MonoBehaviour
{
  [Header("Parameters")]
  [SerializeField] private int hexagon_size_;
  [SerializeField] private float hexagon_point_distance_;
  [SerializeField] private Transform start_position_;

  [Header("Debug")]
  [SerializeField] private GameObject debug_point_;
  [SerializeField] private MeshFilter mesh_filter_;
  
  // privates

  private System.Random rng_;

  private List<Vector3> verts_ = new List<Vector3>();
  // For future me. I use HashSet here to not to deal with collisions. But it might be worth performance-wise to use list idk.
  private Dictionary<Vector3, HashSet<Vector3>> large_vertex_neighbors_ = new Dictionary<Vector3, HashSet<Vector3>>();
  private HashSet<Tuple<Vector3, Vector3>> lines_ = new HashSet<Tuple<Vector3, Vector3>>(new PointComparer());
  private List<Tuple<Vector3, Vector3>> removed_lines_ = new List<Tuple<Vector3, Vector3>>();

  private Vector3[] directions_;
  private Vector3[] steps_;
  private float min_edge_vertex_distance_sqr_;

  // constants

  private const int kHexagonSides = 6;

  private class PointComparer : IEqualityComparer<Tuple<Vector3, Vector3>>
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


  private void Start()
  {
    rng_ = new System.Random();

    //RunTests();

    if(hexagon_size_ < 1)
      return;

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

    GenerateVerts();
    DebugCreateVerts(verts_.ToArray());

    GenerateLines();
    //DebugPrintLinesInfo(lines_.ToArray());

    GenerateLargeVertexNeighbors();

    RemoveRandomLinesToMakeQuads();
  }

  private void Update()
  {
    DebugDrawLines(lines_.ToArray());
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
    foreach (Tuple<Vector3, Vector3> line in lines_)
    {
      if(!large_vertex_neighbors_.ContainsKey(line.Item1))
        large_vertex_neighbors_[line.Item1] = new HashSet<Vector3>();
      if(!large_vertex_neighbors_.ContainsKey(line.Item2))
        large_vertex_neighbors_[line.Item2] = new HashSet<Vector3>();
      large_vertex_neighbors_[line.Item1].Add(line.Item2);
      large_vertex_neighbors_[line.Item2].Add(line.Item1);
    }
  }

  private void GenerateLines()
  {
    float max_radius_sqr = Mathf.Pow(hexagon_point_distance_ * (hexagon_size_ - 1), 2);

    for(int vert_i = 0; vert_i < verts_.Count; ++vert_i)
    {
      for(int side_i = 0; side_i < kHexagonSides; ++side_i)
      {
        if((verts_[vert_i] + directions_[side_i]).sqrMagnitude > max_radius_sqr)
          continue;
        lines_.Add(new Tuple<Vector3, Vector3>(verts_[vert_i], verts_[vert_i] + directions_[side_i]));
      }
    }
  }

  private void RemoveRandomLinesToMakeQuads()
  {
    Tuple<Vector3, Vector3>[] shuffled_lines_ = lines_.ToArray();
    Utilities.ShuffleArray(shuffled_lines_, rng_);

    for(int i = 0; i < shuffled_lines_.Length; ++i)
    {
      if(IsEdgeVertex(shuffled_lines_[i].Item1) && IsEdgeVertex(shuffled_lines_[i].Item2))
        continue;
      if(!DoPointsHave2CommonNeighbors(shuffled_lines_[i].Item1, shuffled_lines_[i].Item2))
        continue;
      lines_.Remove(shuffled_lines_[i]);
      large_vertex_neighbors_[shuffled_lines_[i].Item1].Remove(shuffled_lines_[i].Item2);
      large_vertex_neighbors_[shuffled_lines_[i].Item2].Remove(shuffled_lines_[i].Item1);
      removed_lines_.Add(shuffled_lines_[i]);
    }
  }

  private bool DoPointsHave2CommonNeighbors(Vector3 point1, Vector3 point2)
  {
    int matches = 0;
    foreach(Vector3 point in large_vertex_neighbors_[point1])
    {
      if(large_vertex_neighbors_[point2].Contains(point))
        ++matches;
    }

    return matches == 2;
  }

  private bool IsEdgeVertex(Vector3 point)
  {
    return point.sqrMagnitude >= min_edge_vertex_distance_sqr_;
  }

  private void RunTests()
  {
    IEqualityComparer<Tuple<Vector3, Vector3>> comparer = new PointComparer();
    Debug.Log("PointComparer.Equals test expected true, got: " + comparer.Equals(new Tuple<Vector3, Vector3>(Vector3.one, Vector3.zero), new Tuple<Vector3, Vector3>(Vector3.zero, Vector3.one)));
    Debug.Log("PointComparer.GetHashCoode test expected true, got: " + (comparer.GetHashCode(new Tuple<Vector3, Vector3>(Vector3.one, Vector3.zero)) == comparer.GetHashCode(new Tuple<Vector3, Vector3>(Vector3.zero, Vector3.one))).ToString());
    Debug.Log("PointComparer.Equals test expected true, got: " + comparer.Equals(new Tuple<Vector3, Vector3>(new Vector3(-0.87f, 0.0f, 0.50f), Vector3.zero), new Tuple<Vector3, Vector3>(Vector3.zero, new Vector3(-0.87f, 0.0f, 0.50f))));
    Debug.Log("PointComparer.GetHashCoode test expected true, got: " + (comparer.GetHashCode(new Tuple<Vector3, Vector3>(new Vector3(-0.87f, 0.0f, 0.50f), Vector3.zero)) == comparer.GetHashCode(new Tuple<Vector3, Vector3>(Vector3.zero, new Vector3(-0.87f, 0.0f, 0.50f)))).ToString());
  }

  private void DebugPrintLinesInfo(Tuple<Vector3, Vector3>[] lines)
  {
    Debug.Log(lines.Length);
    for(int i = 0; i < lines.Length; ++i)
      Debug.Log(lines[i].Item1.ToString() + " " + lines[i].Item2.ToString());
  }

  private void DebugCreateVerts(Vector3[] verts)
  {
    for(int i = 0; i < verts.Length; ++i)
    {
      GameObject vertex_object = Instantiate(debug_point_, verts[i], Quaternion.identity);
      vertex_object.name = i.ToString();
    }
  }

  private void DebugDrawLines(Tuple<Vector3, Vector3>[] lines)
  {
    for(int i = 0; i < lines.Length; ++i)
    {
      Debug.DrawLine(lines[i].Item1, lines[i].Item2, Color.red);  
    }
  }
}
