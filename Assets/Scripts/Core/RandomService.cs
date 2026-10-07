using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局随机源。所有概率判定都必须走这里，保证：
/// 1. 同一个种子能复现同一局；
/// 2. 出 bug 时能重放；
/// 3. 概率相关逻辑可以写单测。
/// </summary>
public class RandomService
{
    private System.Random random;

    public int Seed { get; private set; }

    public RandomService(int seed)
    {
        Reseed(seed);
    }

    public void Reseed(int seed)
    {
        Seed = seed;
        random = new System.Random(seed);
    }

    /// <summary>生成一个新的随机种子，用于“再来一局”。</summary>
    public static int NewSeed()
    {
        return unchecked((int)DateTime.UtcNow.Ticks) ^ Guid.NewGuid().GetHashCode();
    }

    /// <summary>[0,1) 之间的随机数。</summary>
    public float Value()
    {
        return (float)random.NextDouble();
    }

    /// <summary>[min,max) 的整数。</summary>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return random.Next(minInclusive, maxExclusive);
    }

    /// <summary>[min,max] 的整数（含两端）。</summary>
    public int RangeInclusive(int minInclusive, int maxInclusive)
    {
        if (maxInclusive <= minInclusive) return minInclusive;
        return random.Next(minInclusive, maxInclusive + 1);
    }

    /// <summary>[min,max) 的浮点数。</summary>
    public float Range(float min, float max)
    {
        if (max <= min) return min;
        return min + (float)random.NextDouble() * (max - min);
    }

    /// <summary>概率判定，probability 会被夹到 [0,1]。</summary>
    public bool Chance(float probability)
    {
        return random.NextDouble() < Mathf.Clamp01(probability);
    }

    /// <summary>按权重取一个下标；权重全为 0 或列表为空时返回 -1。</summary>
    public int WeightedIndex(IList<float> weights)
    {
        if (weights == null || weights.Count == 0) return -1;

        float total = 0f;
        for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i]);
        if (total <= 0f) return -1;

        float roll = Value() * total;
        float cursor = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            cursor += Mathf.Max(0f, weights[i]);
            if (roll < cursor) return i;
        }

        return weights.Count - 1;
    }

    /// <summary>按权重从列表里取一个元素；列表为空返回 default(T)。</summary>
    public T PickWeighted<T>(IList<T> items, IList<float> weights)
    {
        int index = WeightedIndex(weights);
        if (index < 0 || index >= items.Count) return default(T);
        return items[index];
    }

    /// <summary>从列表里随机取一个元素。</summary>
    public T Pick<T>(IList<T> items)
    {
        if (items == null || items.Count == 0) return default(T);
        return items[Range(0, items.Count)];
    }

    /// <summary>是否命中“以 1/count 为概率”的稀有事件，count&lt;=1 时必中。</summary>
    public bool OneIn(int count)
    {
        if (count <= 1) return true;
        return Range(0, count) == 0;
    }
}
