using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using UnityEngine;

public class CustomProjection : MonoBehaviour
{
    // Texture ���μ��� texel ��
    public int width = 128;
    public int height = 128;

    // Volume rendering Mode
    public enum RenderMode
    {
        MaximumIntensityProjection,
        AlphaComposition,
        DistanceLerp
    }
    public RenderMode rendermode;


    public float distance_scale=4; // for DistanceLerp mode
    public float stepSize = 0.3f;
    public GameObject plane;
    public GameObject volumeContainer; // ���� �����͸� ��� cube



    // 2D Texture�� world coordinate�� ������ �迭
    private Vector3[] worldCoordinates;

    private Vector3[] enterPoints;
    private Vector3[] exitPoints;
    private Color[] colors;

    private Material curMat;
    private Texture2D texture;
    private Camera mainCamera;

    private byte[,,] volumeData;


    // raw���� �ҷ�����
    byte[,,] LoadVolumeDataFromFile(string filePath, int width, int height, int depth)
    {
        byte[,,] data = new byte[depth, height, width];

        try
        {
            //Debug.Log("?");
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
            {
                //Debug.Log("??");
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    //Debug.Log("???");
                    // ���Ͽ��� ���� �����͸� �о�� �迭�� ����
                    for (int z = 0; z < depth; z++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                data[z, y, x] = reader.ReadByte();
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("An error occurred while loading volume data: " + e.Message);
        }

        return data;
    }


    // Start is called before the first frame update
    void Start()
    {
        // ���� ��ü(plane)�� renderer ��������
        curMat = GetComponent<Renderer>().material;

        // ī�޶�� plane�� ����
        mainCamera = Camera.main;
        plane.transform.parent = mainCamera.transform;

        // 2D Texture �ʱ�ȭ
        texture = new Texture2D(width, height);

        // enterpoint, exitpoint �ʱ�ȭ
        enterPoints = new Vector3[width * height];
        exitPoints = new Vector3[width * height];

        // worldCoordinate �Ҵ� �� texture color �ʱ�ȭ
        worldCoordinates = new Vector3[width * height];
        colors = new Color[width * height];
        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
            {
                int index = iy * width + ix;
                worldCoordinates[index] = GetWorldCoordinate(new Vector2(ix, iy));
                colors[index] = Color.black;
                //Debug.Log("worldcoordinate of index" + index + "is" + worldCoordinates[index]);
            }
        }

        // VolumeData �ҷ����� ** ����� �����η� ���������� streamingasset �̿��ؼ� �����ؾ� ���߿� Ȧ�η������ �۵���
        //volumeData = LoadVolumeDataFromFile("C:/Users/yjkim/Volume Rendering/Assets/EasyVolumeRendering/StreamingAssets/lung.raw", 256, 256, 128);


        volumeData = LoadVolumeDataFromFile(Application.streamingAssetsPath + "/lung.raw", 256, 256, 128);
        //volumeData = LoadVolumeDataFromFile(Application.streamingAssetsPath + "/bonsai_256_256_256.raw", 256, 256, 256);
        //Debug.Log(Application.streamingAssetsPath);

        // Debug.Log("volumeData_Length: "+volumeData.Length);
    }

    Vector3 GetWorldCoordinate(Vector2 ImgCoord)
    {   
        Vector3 obj_coord = new Vector3(5*(ImgCoord.x / (width/2) - 1), 0, 5*(ImgCoord.y / (height/2) - 1));
        Vector3 world_Coord = transform.TransformPoint(obj_coord);
        return world_Coord ;
    }


    void VolumeRendering()
    {
        // Plane layer�� ray�� ��Ƶ� ����
        int layerMask = (-1) - (1 << LayerMask.NameToLayer("Plane"));

        // For each texture pixel...
        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
            {
                int index = iy * width + ix;
                worldCoordinates[index] = GetWorldCoordinate(new Vector2(width - ix, height - iy)); //�����¿� �����Ǿ��־���

                // ray�� origin�� direction ����
                Vector3 origin = mainCamera.transform.position;
                Vector3 direction = worldCoordinates[index] - origin;

                // maincamera���� �� �ȼ��� ���ϴ� ray
                Ray ray = new Ray(origin, direction);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
                {
                    //Debug.Log("Enter point: " + hit.point);
                    enterPoints[index] = hit.point;

                    // ��ü���� ray�� ������ ���� ã��
                    Ray exitRay = new Ray(hit.point+direction*100.0f, -direction);
                    RaycastHit exitHit;
                    if (Physics.Raycast(exitRay, out exitHit, Mathf.Infinity, layerMask))
                    {
                        exitPoints[index] = exitHit.point;

                        int max_v = 0;

                        int stepNum = Mathf.FloorToInt(Vector3.Distance(exitPoints[index], enterPoints[index]) / stepSize);

                        // alpha��, color�� �ʱ�ȭ (Alpha composition)
                        float alpha = 0.0f;
                        Color color = new Color(0, 0, 0, 0);

                        for(int i = 0; i < stepNum; i++)
                        {
                            // WS -> OS -> VS�� �̵�
                            Vector3 curWorldPoint = enterPoints[index] + (exitPoints[index] - enterPoints[index]) / stepNum * i;
                            Vector3 curLocalPoint = volumeContainer.transform.InverseTransformPoint(curWorldPoint); // [-0.5 ~ 0.5]
                            Vector3 curVolumePoint = new Vector3((curLocalPoint.x + 0.5f) * 255f, (curLocalPoint.y + 0.5f) * 255f, (curLocalPoint.z + 0.5f) * 127f);
                            
                            // Exception handling : curVolumePoint�� z�� Mathf.FloorToInt ����� -1�� ����.
                            try
                            {
                                byte intensity = volumeData[Mathf.FloorToInt(curVolumePoint.z), Mathf.FloorToInt(curVolumePoint.y), Mathf.FloorToInt(curVolumePoint.x)];
                                
                                // Maximum Intensity Projection
                                max_v = (max_v < intensity) ? intensity : max_v;

                                // Alpha composition
                                Color sampleColor = new Color(intensity / 255.0f, intensity / 255.0f, intensity / 255.0f, intensity / 255.0f);
                                color = color + (1 - alpha) * sampleColor * sampleColor.a;
                                alpha = alpha + (1 - alpha) * sampleColor.a;
                                
                            }
                            catch (Exception e)
                            {
                                //Debug.Log(e.Message);
                                //Debug.Log(Mathf.FloorToInt(curVolumePoint.z));
                            }

                        }


                        // scene���� rendermode ���� ����
                        RenderMode mode = GetRenderMode();

                        switch (mode)
                        {
                            case RenderMode.MaximumIntensityProjection:
                                {
                                    // Maximum Intensity Projection
                                    colors[index] = new Color(max_v / 255.0f, max_v / 255.0f, max_v / 255.0f, max_v / 255.0f);
                                    break;
                                }
                            case RenderMode.AlphaComposition:
                                {
                                    // Alpha composition (Front-to-back)
                                    colors[index] = color;
                                    break;
                                }
                            case RenderMode.DistanceLerp:
                                {
                                    //** ���������� �ҷ����� �������� �׳� distance�� color interpolation�ؼ� ���� ������. (distance_scale�� ���� ����)
                                    float distance = Vector3.Distance(enterPoints[index], exitPoints[index]);
                                    colors[index] = Color.Lerp(Color.white, Color.blue, distance / distance_scale);
                                    break;
                                }
                        }




                    }
                }
            }
        }
    }

    RenderMode GetRenderMode()
    {
        return rendermode;
    }
    
    void ClearTexture()
    {
        for(int i = 0; i < width*height; i++)
        {
            colors[i] = Color.black;
        }
    }

    // Update is called once per frame
    void Update()
    {
        ClearTexture();

        mainCamera = Camera.main;
        
        VolumeRendering();
        
        texture.SetPixels(colors);
        texture.Apply();
        curMat.mainTexture = texture;
    }

}
