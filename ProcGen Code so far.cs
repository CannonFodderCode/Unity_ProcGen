using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using JetBrains.Annotations;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public bool fixSeed;
    private const int mapWidth = 100;
    private const int mapHeight = 100;
    private float[,] noiseMap = new float[mapHeight, mapWidth];
    private float[,] activeMap = new float[mapHeight, mapWidth];
    public float renderScale = 0.1f;
    public float noiseGenScale = 0.01f;
    public float threshold = 0.5f;
    public GameObject wallBlock;
    private System.Random seed = new System.Random();

    private List<AreaMapZone> savedZones = new List<AreaMapZone>();

    public TextMeshProUGUI statsReadout;

    int[][] adjacentSquares = new int[][]
        {// Used to check the surrounding 4 squares for gaps
            new int[] {-1, 0},
            new int[] {1, 0},
            new int[] {0, -1},
            new int[] {0, 1},
        };
    // Start is called before the first frame update
    void Start()
    {
        activeMap = GenerateMapData(mapWidth, mapHeight, noiseGenScale);
        activeMap = SortIslands(activeMap);
        activeMap = CheckPaths(activeMap);
        BuildMap(activeMap);
        Debug.Log("Total map zones: " + savedZones.Count);
        //checkforgaps();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
    public float[,] GenerateMapData(int mapHeight, int mapWidth, float scale)
    {
        var xSeed = 0; var zSeed = 0; // Apparently needs defining here to avoid constraining it to the following logic block. C# is strange...
        if (fixSeed)  // DEBUG - switch to 1 to lock seed in place
        {
            xSeed = 0; zSeed = 0;
        }
        else
        {
            xSeed = seed.Next(-10000, 10000);
            zSeed = seed.Next(-10000, 10000);
        }

        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                // Generate noise
                float sample = Mathf.PerlinNoise((x * scale) + xSeed, (z * scale) + zSeed);

                // Test here
                var center = ((float)mapWidth / 2, (float)mapHeight / 2);
                var currentCoord = (x, z);
                // Calculates the distance of the current coordinate to the center of the map
                var distToCenter = Mathf.Sqrt(Mathf.Pow((center.Item1 - currentCoord.Item1) / (float)mapWidth, 2) + Mathf.Pow((center.Item2 - currentCoord.Item2) / (float)mapHeight, 2)) / (Math.Sqrt(2) / 1.5f);
                // 1 is far, 0 is close


                if (x == 2 && z == 0)
                {
                    Debug.Log(distToCenter + " is the distance to center for the below calculation:");
                    Debug.Log(sample + " Multiplied by " + ((float)Math.Pow((2 * distToCenter) - 0.7f, 7) + 1) + " = " + sample * ((float)Math.Pow((2 * distToCenter) - 0.7f, 7) + 1));
                }
                sample *= ((float)Math.Pow((2 * distToCenter) + 0.3, 7) + 1);
                //Save the noise to the grid coordinates
                noiseMap[x, z] = sample;
            }
        }
        Debug.Log(noiseMap[0, 0]);
        return noiseMap;
    }
    public void BuildMap(float[,] mapToBuild)
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                if (mapToBuild[x, z] >= threshold) // allows for the threashold of
                {
                    // Place the top surface:
                    Instantiate(wallBlock, new Vector3(x * renderScale, renderScale - 1, z * renderScale), new Quaternion(0, 0, 0, 0)).transform.localScale *= renderScale;

                    foreach (var check in adjacentSquares)
                    {
                        var xCheck = x + check[0];
                        var zCheck = z + check[1];
                        if (xCheck > 0 && zCheck > 0 && xCheck < mapWidth && zCheck < mapHeight)
                        {
                            if (mapToBuild[xCheck, zCheck] < threshold)
                            {
                                // Set the position and rotation of the side planes
                                var placement = new Vector3(x * renderScale + (renderScale / 2) * check[0], renderScale / 2 - 1, z * renderScale + (renderScale / 2) * check[1]);
                                var rotation = Quaternion.Euler(check[1] * 90, 0, check[0] * -90);

                                // Create and scale side planes to cover the exposed sides.
                                Instantiate(wallBlock, placement, rotation).transform.localScale *= renderScale;
                            }
                        }
                    }
                }
            }
        }
    }
    public float[,] SortIslands(float[,] mapToBuild)
    {
        // Create the temporary storage for island checks
        HashSet<Tuple<int, int>> tempStorage = new HashSet<Tuple<int, int>>();
        HashSet<Tuple<int, int>> toAdd = new HashSet<Tuple<int, int>>();
        HashSet<Tuple<int, int>> toRemove = new HashSet<Tuple<int, int>>();


        bool edited;
        // Create Objets per island
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                if (mapToBuild[x, z] < threshold)
                {// Square is empty - check if already grouped
                    bool newZoneFound = true;
                    foreach (var zone in savedZones)
                    {
                        if (zone.spaces.Contains(new Tuple<int, int>(x, z)))
                        {// Square was found in a pre-created object
                            newZoneFound = false;
                            break;
                        }
                        else
                        {
                            // Create and file a new object, adding all squares contained inside to its data
                        }
                    }
                    if (newZoneFound)
                    {// Creating and filing a new zone with all connected squares
                        tempStorage.Add(new Tuple<int, int>(x, z));
                        var newZone = new AreaMapZone();
                        savedZones.Add(newZone);
                        newZone.ID = savedZones.Count;
                        newZone.spaces.Add(new Tuple<int, int>(x, z)); // adds the first square to the list of "owned" spaces

                        // Check adjacent squares
                        edited = true;
                        while (edited)
                        {
                            edited = false;
                            toAdd.Clear();
                            toRemove.Clear();
                            foreach (var square in tempStorage)
                            {
                                foreach (var check in adjacentSquares)
                                {
                                    var xCheck = square.Item1 + check[0];
                                    var zCheck = square.Item2 + check[1];
                                    var coordCheck = new Tuple<int, int>(xCheck, zCheck);
                                    if (!tempStorage.Contains(coordCheck) && !newZone.spaces.Contains(coordCheck))
                                    {
                                        if (mapToBuild[xCheck, zCheck] < threshold)
                                        {// Is outside the shape and sub-threshold
                                            edited = true;
                                            toAdd.Add(coordCheck);
                                            newZone.spaces.Add(coordCheck);
                                        }
                                        else
                                        {// Is outsiude the shape, but above threshold - is an edge
                                            newZone.edges.Add(new Tuple<int, int>(square.Item1, square.Item2));
                                        }
                                    }
                                }
                                toRemove.Add(square);
                            }
                            tempStorage.Clear();
                            foreach (var item in toAdd)
                            {
                                tempStorage.Add(item);
                            }
                        }
                    }
                }
            }
        }
        var displayText = "stats: \n";
        foreach (var island in savedZones)
        {// Update the size of the islands
            island.size = island.spaces.Count;
            Debug.Log($"island has {island.size} spaces and a perimiter of {island.edges.Count}");
            displayText += $"island has {island.size} spaces and a perimiter of {island.edges.Count}\n";
        }
        statsReadout.text = $"{savedZones.Count} Islands have been created\n\n";
        statsReadout.text += displayText;
        return mapToBuild;
    }
    public float[,] CheckPaths(float[,] mapToBuild)
    {
        if (savedZones.Count > 1)
        {
            foreach (var zone in savedZones)
            {
                ;
            }
        }



        return mapToBuild;
    }
    public void checkforgaps()
    {// Used to highlight all zoned tiles to see if any are missed - DEBUG OPTION
        foreach (var zone in savedZones)
        {
            foreach (var coordinate in zone.spaces)
            {
                var x = coordinate.Item1;
                var z = coordinate.Item2;
                var thing = Instantiate(wallBlock, new Vector3(x * renderScale, renderScale - 1, z * renderScale), new Quaternion(0, 0, 0, 0));
                thing.transform.localScale *= renderScale;
                thing.GetComponent<Renderer>().material.color = Color.red;
            }
        }
    }
}

public class AreaMapZone
{
    public int ID;
    public int size;
    public Tuple<int, int> location;
    public HashSet<Tuple<int, int>> spaces = new HashSet<Tuple<int, int>>();
    public HashSet<Tuple<int, int>> edges = new HashSet<Tuple<int, int>>();
}
