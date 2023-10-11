using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using static TreeEditor.TextureAtlas;

public class DecompWindow : EditorWindow
{

    public UnityEngine.Object[] allJsonFiles;
    public Texture2D[] allAtlasTextures;
    public string exportPath = "Assets/";
    public bool addSubDirs = true, debugging, makeMesh;
    public int debugMaxIterations = -1;

    public Vector2 scrollPos;

    [MenuItem("Bluey Let's Play/Decompile Atlas")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(DecompWindow));
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Input Content:");

        //Array field
        ScriptableObject scriptableObj = this;
        SerializedObject serialObj = new SerializedObject(scriptableObj);
        SerializedProperty serialJson = serialObj.FindProperty("allJsonFiles");

        EditorGUILayout.PropertyField(serialJson, true);
        serialObj.ApplyModifiedProperties();
        //-----

        GUILayout.Space(5);

        //Array field
        ScriptableObject scriptableObj2 = this;
        SerializedObject serialObj2 = new SerializedObject(scriptableObj2);
        SerializedProperty serialJson2 = serialObj2.FindProperty("allAtlasTextures");

        EditorGUILayout.PropertyField(serialJson2, true);
        serialObj2.ApplyModifiedProperties();
        //-----

        GUILayout.Space(5);

        GUILayout.Label("Export Path:");
        exportPath = GUILayout.TextField(exportPath);

        if (GUILayout.Button("Set Path to Selected")) {
            SetPath();
        }

        addSubDirs = GUILayout.Toggle(addSubDirs, "Create Folders For Each Atlas");
        makeMesh = GUILayout.Toggle(makeMesh, "Generate Meshes");

        GUILayout.Space(20);

        if (GUILayout.Button("Decompile"))
        {
            BatchDecompile();
        }

        debugging = GUILayout.Toggle(debugging, "Debug");
        if (debugging) {
            GUILayout.Label("max iterations (-1 disables)");
            debugMaxIterations = EditorGUILayout.IntField(debugMaxIterations);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    public void SetPath()
    {
        var Getpath = "";
        var obj = Selection.activeObject;
        if (obj == null) Getpath = "Assets";
        else Getpath = AssetDatabase.GetAssetPath(obj.GetInstanceID());
        if (Getpath.Length > 0) {
            if (Directory.Exists(Getpath)) {
                exportPath = Getpath + "/";
            }
        }
    }

    public void BatchDecompile()
    {
        int i = 0;
        foreach (UnityEngine.Object atlas in allJsonFiles)
        {
            Decompile(atlas, allAtlasTextures[i]);
            i++;
        }
    }

    public void Decompile(UnityEngine.Object atlas, Texture2D atlasTex)
    {
        string data = "";

        StreamReader reader = new StreamReader(AssetDatabase.GetAssetPath(atlas));

        while (!reader.EndOfStream)
        {
            data += "\n" + reader.ReadLine();
        }

        string[] dataArray = data.Split('\n');


        int i = 0, l = 0, subtexIndex = -1;
        int mode = -1;
        bool uvReady = false;
        bool counting = false;
        // _____________
        // | M O D E S | 
        // ---TEXTURE---
        //-1 = counting
        //0 = header info (ignored)
        //1 = checking values
        //2 = reading uvs
        //3 = proccess uvs
        //4 = footer info (texture name)
        // ----MESH-----
        //5 = vertex data
        //6 = indicies
        // -------------
        bool vCount = false, iCollect = false;
        int vIndex = 0;

        Transform root = new GameObject().transform;
        root.gameObject.name = "DataRoot";

        List<atlasOutput> textures = new List<atlasOutput>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector2> meshUvs = new List<Vector2>();
        List<string> spriteNames = new List<string>();

        List<Vector3> verticies = new List<Vector3>();
        List<int> indicies = new List<int>();

        int iterator = debugMaxIterations;

        foreach (string content in dataArray)
        {
            if(iterator == 0) { break; }

            if (mode != -1 && subtexIndex != -1)
            {
                EditorUtility.DisplayProgressBar("Progress:", spriteNames[subtexIndex], spriteNames.Count / l);
            }

            if (mode == -1 && content.Contains("_keys"))
            {
                counting = true;
                spriteNames.Clear();
            }
            else if(mode == -1 && counting)
            {
                if (content.Contains("]")) {
                    mode = 0;
                    counting = false;
                } else {
                    spriteNames.Add(content.Substring(1, content.Length - 2));
                }
            }
            else if (mode == 0 && content.Contains("_values")) { //find atlas subtexture
                mode = 1;
                subtexIndex++;
                iterator--;
                atlasOutput go = new GameObject().AddComponent<atlasOutput>();
                go.transform.parent = root;

                textures.Add(go);
                Log("FOUND SUBTEXTURE!\n" + l);
            }
            else if (mode == 1 && content.Contains("Name")) //assign name to subtexture
            {
                mode = 2;
                string[] subarray = content.Split('"');
                textures[textures.Count -1].name = subarray[3];

                Log("FOUND SUBTEXTURE NAME! <b>( "+ subarray[3] +" )</b>\n" + l);

                if (makeMesh) //skip to 5 to find vertex info
                {
                    mode = 5;
                }
            }
            else if(mode == 5 && content.Contains("Vertices")) 
            {
                Log("FOUND VERTEX INFO!\n" + l);
                vCount = true;
            }
            else if (mode == 5 && vCount)
            {
                if (content.Contains("]")) {
                    mode = 2;
                    vCount = false;
                }
                else {
                    if (vIndex == 0) {

                        vIndex = 4;
                        Vector3 vertexInfo;
                        string x, y, z;
                        
                        x = dataArray[l + 1].Substring(21, dataArray[l + 1].Length - 22);
                        y = dataArray[l + 2].Substring(21, dataArray[l + 2].Length - 22);
                        z = dataArray[l + 3].Substring(21, dataArray[l + 3].Length - 21);

                        Log("<color=orange>VERTEX CHUNK!\n" + x + " | " + y + " | " + z + "</color>");

                        vertexInfo.x = float.Parse(x);
                        vertexInfo.y = float.Parse(y);
                        vertexInfo.z = float.Parse(z);

                        verticies.Add(vertexInfo);
                    } else {
                        vIndex--;
                    }
                }
                
            }
            else if (mode == 2 && !uvReady && content.Contains("Uvs")) //find start to UV data
            {
                uvReady = true;
                uvs.Clear();

                Log("FOUND UV DATA!\n" + l);
            }
            else if (mode == 2 && uvReady && i == 0 && !content.Contains("]")) //find UV chunk
            {
                string chunk = dataArray[l] + dataArray[l+1] + dataArray[l+2] + dataArray[l+3];

                Log("<color=cyan>UV CHUNK!\n" + chunk + "</color>");

                Vector2 chunkData = new Vector2();

                string x = dataArray[l + 1].Substring(21, dataArray[l + 1].Length - 22);
                string y = dataArray[l + 2].Substring(21, dataArray[l + 2].Length - 21);

                Log(x + " | " + y);

                chunkData.x = float.Parse(x);

                chunkData.y = float.Parse(y);

                uvs.Add(chunkData);
                meshUvs.Add(chunkData);

                i++;
            }
            else if (mode == 2 && uvReady && i != 0) { //await next UV chunk
                i++;
                if (i == 4) {
                    i = 0;
                }
            }
            else if (mode == 2 && uvReady && (content.Contains("]"))) //end of UV data
            {
                uvReady = false;
                i = 0;
                mode = 3;

                if (makeMesh) { mode = 6; }
                Log("FOUND END OF UV DATA!\n" + l);
            }
            else if(mode == 6 && content.Contains("Indices"))
            {
                Log("FOUND INDICIES!\n" + l);
                iCollect = true;
            }
            else if (mode == 6 && iCollect)
            {
                if (content.Contains("]")) {
                    iCollect = false;
                    mode = 3;
                }
                else {

                    string fixedString = content;
                    if (content.Contains(",")) {
                        fixedString = content.Substring(0, content.Length - 1);
                    }

                    Log("<color=lime> LITTLE FELLA! :D \n" + fixedString + "</color>");

                    indicies.Add(int.Parse(fixedString));
                }
            }
            else if(mode == 3) //process uvs & mesh
            {
                if (makeMesh)
                {
                    GameObject rootGo = GameObject.Find(atlasTex.name);
                    if (rootGo == null)
                    {
                        rootGo = new GameObject();
                        rootGo.name = atlasTex.name;
                    }

                    MeshFilter filter = new GameObject().AddComponent<MeshFilter>();
                    filter.gameObject.AddComponent<MeshRenderer>();
                    filter.transform.parent = rootGo.transform;

                    Mesh mesh = new Mesh();
                    filter.mesh = mesh;
                    mesh.vertices = verticies.ToArray();
                    mesh.SetIndices(indicies.ToArray(), MeshTopology.Triangles, 0);
                    mesh.uv = meshUvs.ToArray();
                    /*mesh.RecalculateBounds();
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();*/
                    mesh.name = atlasTex.name + "_" + textures[0].name;
                    filter.gameObject.name = textures[0].name;
                }

                int length = (uvs.Count - 1) / 4;
                
                for (int j = 0; j < length; j++) //for each subtexture
                {
                    //find width and height in UV space

                    

                    float uvWidth = 0, uvHeight = 0;
                    Vector2 startPix = new Vector2(1, 1);

                    for (int t = 0; t < 4; t++) //for each uv chunk (4 per tex)
                    {
                        float x = uvs[j + t].x;
                        float y = uvs[j + t].y;

                        if (x > uvWidth) {
                            uvWidth = x;
                        }
                        if (x < startPix.x) {
                            startPix.x = x;
                        }

                        if (y > uvHeight) {
                            uvHeight = y;
                        }
                        if (y < startPix.y) {
                            startPix.y = y;
                        }
                    }

                    int pixelWidth = Mathf.RoundToInt(atlasTex.width * uvWidth);
                    int pixelHeight = Mathf.RoundToInt(atlasTex.height * uvHeight);

                    //convert to texture space & create texture with the pixel dimensions
                    Texture2D subTex = new Texture2D(pixelWidth, pixelHeight);

                    //Log("<color=yellow>CHECK DIMENSIONS: " + pixelWidth * pixelHeight + " | " + CreateSubtex(uvs, atlasTex, new Vector2(pixelWidth, pixelHeight), startPix).Length + "</color>");

                    subTex.SetPixels(CreateSubtex(uvs, atlasTex, new Vector2(pixelWidth, pixelHeight), startPix));

                    //Output file
                    byte[] tex = subTex.EncodeToPNG();

                    int randomNum = UnityEngine.Random.Range(0, 9999);

                    string pathAppend = "";

                    //create subfolder
                    if (!Directory.Exists(exportPath + atlasTex.name + "/") && addSubDirs) {
                        Directory.CreateDirectory(exportPath + atlasTex.name);
                        pathAppend = atlasTex.name + "/";
                    }
                    else if (addSubDirs)
                    {
                        pathAppend = atlasTex.name + "/";
                    }

                    FileStream stream = new FileStream(exportPath + pathAppend + textures[0].name + "_" + j + ".png", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    BinaryWriter writer = new BinaryWriter(stream);
                    for (int w = 0; w < tex.Length; w++)
                        writer.Write(tex[w]);

                    writer.Close(); 
                    stream.Close();


                    AssetDatabase.ImportAsset(exportPath + pathAppend + textures[0].name + "_" + j + ".png", ImportAssetOptions.ForceUpdate);

                }

                //textures.Clear();
                mode = 1; //reset for next tex
            }

            l++;
            if (debugging) Debug.Log(i + " - " + content);
        }

        DestroyImmediate(root.gameObject);
        EditorUtility.ClearProgressBar();
        //Debug.Log(data);
    }

    Color[] CreateSubtex(List<Vector2> uvs, Texture2D atlas, Vector2 Dimensions, Vector2 StartPixel)
    {
        Log("CREATING SUBTEXTURE! Startpos: " + Mathf.RoundToInt(StartPixel.x * Dimensions.x) + ", " + Mathf.RoundToInt(StartPixel.y * Dimensions.y));
        Color[] col = new Color[Mathf.RoundToInt(Dimensions.x * Dimensions.y)];

        int i = 0;
        for (int y = 0; y < Dimensions.y; y++)
        {
            for (int x = 0; x < Dimensions.x; x++)
            {
                col[i] = new Color();
                col[i] = atlas.GetPixel(Mathf.RoundToInt(StartPixel.y * Dimensions.y) + y, Mathf.RoundToInt(StartPixel.x * Dimensions.x) + x);

                i++;
            }
            //i++;
        }

        return col;
    }


    void Log(string log)
    {
        if (debugging) {
            Debug.Log("<color=lightblue>" + log + "</color>");
        }
    }
}
