using System;

public class Utilities
{
  public static void ShuffleArray<T>(T[] array, Random rng)
  {
    for(int i = array.Length - 1; i > 0; --i)
    {
      int j = rng.Next(i);
      (array[i], array[j]) = (array[j], array[i]);
    }
  }
}