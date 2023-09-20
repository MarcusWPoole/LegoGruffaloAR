using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;

public class ARSessionController : MonoBehaviour
{
    
    [Header("Text Fields")]
    [SerializeField] private TMP_Text imageText;
    [SerializeField] private TMP_Text rayCastTest;

    [Header("Managers")]
    [SerializeField] private ARTrackedImageManager arImageManager;
    [SerializeField] private ARRaycastManager arRaycastManager;
    [SerializeField] private ARPlaneManager arPlaneManager;
    [SerializeField] private ARPointCloudManager arPointManager;

    [Header("Raycasting")]
    [SerializeField] private GameObject nextSceneButton;
    [SerializeField] private Camera arCamera;

    [Header("Scenes And Scene Audio")]
    [SerializeField] private List<GameObject> scenes;
    [SerializeField] private List<GameObject> additionalSceneObjects;
    [SerializeField] private List<AudioClip> sceneAudio;
    [SerializeField] private GameObject brickTouchEffect;

    // GameObjects and Components
    private Animation sceneAnimation;
    private AudioSource sceneAudioNarration;
    private Dictionary<GameObject, GameObject> scenesAndFood;
    public float timeTillFood = 30;
    public int maxItems = 6;
    public int minItems = 3;
    public GameObject[] draggables;

    // Scene Progression Variables
    private bool readyForNextScene;
    private bool imageFound;
    private bool sceneConstructed;
    private bool buttonDisplayed;
    private bool storyLock;
    private bool extraObjectsAdded;
    public GameObject currentScene;
    private Vector3 tempPos = new Vector3(0f, 0f, 0f);
    private Quaternion tempRot = new Quaternion(0, 0, 0, 0);
    private int sceneAndAudioControlCounter;
    private string referenceImageName;

    // Manager Variables
    private List<ARPlane> activePlanes;
    private List<ARPointCloud> activeClouds;

    // Raycasting Variables
    private GameObject spawnedButton;
    private Dictionary<string, GameObject> arObjects = new Dictionary<string, GameObject>();
    private Vector3 pos = new Vector3(0f, 0.1f, 0f);
    private Quaternion buttonRotation = new Quaternion(0, 0, 0, 0);
    private Transform spawnTransform;

    private float dragDistance;
    private bool isDragged;
    private Vector3 dragOffset;
    private Transform objectToDrag;


    [SerializeField] private ARCameraManager _arCameraManager;
    [SerializeField] private Light _light;
    [SerializeField] private Image _imageLighting;
    
    // Scene Plane/Marker Detection Strings
    private string markerDetected = "Marker Detected";
    private string planeFound = "Plane Found";

    void Awake() {

        arImageManager = GetComponent<ARTrackedImageManager>();

    }
    
    // Start is called before the first frame update
    void Start()
    {
        sceneAudioNarration = GetComponent<AudioSource>();
        sceneAnimation = scenes[0].GetComponent<Animation>();
   
        readyForNextScene = isDragged = imageFound = sceneConstructed = buttonDisplayed = storyLock = extraObjectsAdded = false;
        currentScene = spawnedButton = null;
        sceneAndAudioControlCounter = 0;

        scenesAndFood = new Dictionary<GameObject, GameObject>();
        for (int i = 0; i < (scenes.Count - 1); i++)
        {
            scenesAndFood.Add(scenes[i], additionalSceneObjects[i]);
        }

        draggables = new GameObject[3];
    }

    // Update is called once per frame
    void Update()
    {        
        Vector3 tempVect;

        if (Input.touchCount != 1)
        {
            isDragged = false;
        }
        
        // Handle screen touches - this is the RayTracing Code
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
             
            if (touch.phase == TouchPhase.Began)
            {
                Ray ray = arCamera.ScreenPointToRay(touch.position);
                RaycastHit hitObject;
               // if the raycast hits an object
                if(Physics.Raycast(ray, out hitObject))
                {
                    // raycast hits playbutton
                    if(hitObject.transform.CompareTag("PlayButton"))
                    {
                        readyForNextScene = true;
                    }
                    // raycast hits draggable
                    if(hitObject.transform.CompareTag("Draggable"))
                    {
                        objectToDrag = hitObject.transform;
                        dragDistance = hitObject.transform.position.z - arCamera.transform.position.z;
                        tempVect = new Vector3(touch.position.x, touch.position.y, dragDistance);
                        tempVect = arCamera.ScreenToWorldPoint(tempVect);
                        dragOffset = objectToDrag.position - tempVect;
                        isDragged = true;
                    }
                }
            }

            if (isDragged && touch.phase == TouchPhase.Moved)
            {
                tempVect = new Vector3(touch.position.x, touch.position.y, dragDistance);
                tempVect = arCamera.ScreenToWorldPoint(tempVect);
                objectToDrag.position = tempVect + dragOffset;
            }

            if (isDragged && (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
            {
                isDragged = false;
            }
        }


        if (imageFound)
        {
            if (!storyLock)
            {
                StartCoroutine(QueueNextScene());
            }
        }
    }

    //callback
    void OnEnable() 
    {
        arPlaneManager.planesChanged += OnPlanesChanged;
        arPointManager.pointCloudsChanged += OnPointCloudsChanged;
        arImageManager.trackedImagesChanged += OnTrackedImagesChange;
        //callback to light estimation
        _arCameraManager.frameReceived+=getlight;
    }
    
    void OnDisable() 
    {
        arPlaneManager.planesChanged -= OnPlanesChanged;
        arPointManager.pointCloudsChanged -= OnPointCloudsChanged;
        arImageManager.trackedImagesChanged -= OnTrackedImagesChange;
        _arCameraManager.frameReceived-=getlight;
    }

    //Light estimation function
    void getlight(ARCameraFrameEventArgs args)
    {
        //check if variables are empty
        if (args.lightEstimation.mainLightColor.HasValue && args.lightEstimation.averageBrightness.HasValue && args.lightEstimation.averageColorTemperature.HasValue)
        {
            //assign attributes to each variabl
            _light.color = Color.blue;
            _light.intensity = Mathf.PingPong(Time.time, 8);
            _light.colorTemperature = 2700; //kelvins
            _imageLighting.color = _light.color;
        }
    }
    
    // This method controls the tracked image, and instantiates the first scene and button

    private void OnTrackedImagesChange(ARTrackedImagesChangedEventArgs imageEvent)
    {
        foreach (var newImage in imageEvent.added)
        {
            imageText.text = markerDetected;

            referenceImageName = newImage.referenceImage.name;

            spawnedButton = nextSceneButton;
    // If the button prefab currently doesn't exist then instantiate it above the scene.
            if (string.Compare(spawnedButton.name, referenceImageName, StringComparison.OrdinalIgnoreCase) == 0 && !arObjects.ContainsKey(referenceImageName))
            {                    
                buttonRotation = new Quaternion(0, 90, 0, 0);    
                GameObject newButton = Instantiate(spawnedButton, newImage.transform.position + pos, buttonRotation);
                arObjects[referenceImageName] = newButton;
            }
        // Instantiate first scene
            currentScene = Instantiate(scenes[0], newImage.transform.position, newImage.transform.rotation);
            imageFound = true;
        }

        foreach (var updatedImage in imageEvent.updated)
        {
            currentScene.transform.position = updatedImage.transform.position;
            currentScene.transform.rotation = updatedImage.transform.rotation;

            //update the location and rotation of the button.
            arObjects[referenceImageName].transform.position = updatedImage.transform.position + pos;
            arObjects[referenceImageName].transform.rotation = buttonRotation;
        }

        foreach (var removedImage in imageEvent.removed)
        {
            Destroy(arObjects[removedImage.referenceImage.name]);

            arObjects.Remove(removedImage.referenceImage.name);
        }

    }

    // This method handles all scenes, animations, narrations and scene transitions

    IEnumerator QueueNextScene()
    {
        storyLock = true;

        // Display the next scene button, wand wait for the user to click on it
        if (!readyForNextScene && !buttonDisplayed)
        {
            ShowButton();
            buttonDisplayed = true;
        }
        yield return new WaitWhile(() => readyForNextScene == false);
        HideButton(buttonDisplayed);

        // Construct the next scene, and remove the previous one
        if (!sceneConstructed)
        {
            tempPos = currentScene.transform.position;
            tempRot = currentScene.transform.rotation;
            Destroy(currentScene);
            currentScene = Instantiate(scenes[sceneAndAudioControlCounter], tempPos, tempRot);
            sceneConstructed = true;
        }
        
        // Play the scene's animation
        if (!currentScene.GetComponent<Animation>().isPlaying)
        {
            currentScene.GetComponent<Animation>().Play();
        }

        // Play the scene's narration
        if (!sceneAudioNarration.isPlaying)
        {
            sceneAudioNarration.clip = sceneAudio[sceneAndAudioControlCounter];
            sceneAudioNarration.Play();
        }

        // Add the extra items to scenes 1 - 3
        if (sceneAndAudioControlCounter < 3)
        {
            yield return new WaitForSeconds(timeTillFood);

            if (!extraObjectsAdded)
            {
                Vector3 itemElevation = new Vector3(0.0f, 0.5f, 0.0f);
            
                for (int i = 0; i < 3; i++)
                {
                    draggables[i] = Instantiate(scenesAndFood[scenes[sceneAndAudioControlCounter]], currentScene.transform.position + itemElevation, currentScene.transform.rotation);
                }

                extraObjectsAdded = true;
            }
            
            float timeLeft = sceneAnimation.clip.length - timeTillFood;
            
            yield return new WaitForSeconds(timeLeft);

            for (int j = 0; j < 3; j++)
            {
                Destroy(draggables[j]);
            }

            draggables = new GameObject[3];
        }
        else
        {
            yield return new WaitForSeconds(sceneAnimation.clip.length);
        }

        // Reset variables for the next scene
        sceneAndAudioControlCounter++;
        storyLock = readyForNextScene = sceneConstructed = extraObjectsAdded = false;
        StopCoroutine(QueueNextScene());
    }

// this sets the button to active - when it is instantiated it is disabled by default.
    private void ShowButton()
    {
        arObjects[referenceImageName].SetActive(true);
        arObjects[referenceImageName].transform.position = currentScene.transform.position + pos;
        arObjects[referenceImageName].transform.rotation = buttonRotation;
    }
 // Hides button, plays the particle effect,   
    private void HideButton(bool playEffect)
    {
        if (playEffect)
        {
            GameObject deleteEffect = Instantiate(brickTouchEffect, currentScene.transform.position + pos, buttonRotation);
            Destroy(deleteEffect, 3.0f);
        }
        arObjects[referenceImageName].SetActive(false);
        buttonDisplayed = false;
    }

     private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // handle added planes
        foreach (ARPlane plane in args.added)
        {
            rayCastTest.text = planeFound;
            if (!activePlanes.Contains(plane))
            {
                activePlanes.Add(plane);
            }
        }

        // handle removed planes
        foreach (ARPlane plane in args.removed)
        {
            if (activePlanes.Contains(plane))
            {
                activePlanes.Remove(plane);
            }
        }

        // handle merged planes
        foreach (ARPlane plane in args.updated)
        {
            if (plane.subsumedBy != null && activePlanes.Contains(plane.subsumedBy))
            {
                activePlanes.Remove(plane);
            }
            else if (plane.subsumedBy == null && !activePlanes.Contains(plane))
            {
                activePlanes.Add(plane);
            }
        }
    }

    private void OnPointCloudsChanged(ARPointCloudChangedEventArgs args)
    {
        // handle added clouds
        foreach (ARPointCloud cloud in args.added)
        {
            if (!activeClouds.Contains(cloud))
            {
                activeClouds.Add(cloud);
            }
        }

        // handle removed clouds
        foreach (ARPointCloud cloud in args.removed)
        {
            if (activeClouds.Contains(cloud))
            {
                activeClouds.Remove(cloud);
            }
        }

        // handle updated clouds
        foreach (ARPointCloud cloud in args.updated)
        {
            if (activeClouds.Contains(cloud))
            {
                int index = activeClouds.IndexOf(cloud);
                if (index != -1)
                {
                    activeClouds[index] = cloud;
                }
            }
        }
    }



}
