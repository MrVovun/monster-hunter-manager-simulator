using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterLibrary", menuName = "Guild Manager/Monster Library")]
public class MonsterLibrary : ScriptableObject
{
    [SerializeField] private List<MonsterData> monsters = new List<MonsterData>();

    public List<MonsterData> GetMonsters()
    {
        return monsters;
    }
}
