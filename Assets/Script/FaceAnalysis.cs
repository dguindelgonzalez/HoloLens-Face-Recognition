using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class FaceAnalysis : MonoBehaviour
{
    #region Private Fields

    /// <summary>
    /// Bytes of the image captured with camera
    /// </summary>
    internal byte[] imageBytes;

    /// <summary>
    /// Path of the image captured with camera
    /// </summary>
    internal string imagePath;

    /// <summary>
    /// Base endpoint of Face Recognition Service
    /// </summary>
    const string baseEndpoint = "https://westus.api.cognitive.microsoft.com/face/v1.0/";

    /// <summary>
    /// Auth key of Face Recognition Service
    /// </summary>
    private const string key = "******PUT YOUR KEY*****";

    /// <summary>
    /// Id (name) of the created person group 
    /// </summary>
    private const string personGroupId = "****YOUR GROUP****";
    #endregion

    #region Public Poperties
    public enum ApplicationStateType
    {
        TipPanelState = 1,
        FaceRecognitionState = 2,
    }

    public static ApplicationStateType ApplicationState
    {
        get { return _applicationState; }
        set
        {
            _applicationState = value;
        }
    }

    public Transform TipsPanel;

    /// <summary>
    /// Allows this class to behave like a singleton
    /// </summary>
    public static FaceAnalysis Instance;

    /// <summary>
    /// The analysis result text
    /// </summary>
    public Transform labelPrefab;
    public GameObject cursor;
    [HideInInspector]
    public Transform lastLabelPlaced;
    [HideInInspector]
    public TextMesh lastLabelPlacedText;
    private static ApplicationStateType _applicationState;
    #endregion

    #region Unity Default Methods
    /// <summary>
    /// Initialises this class
    /// </summary>
    private void Awake()
    {
        // Allows this instance to behave like a singleton
        Instance = this;

        // Add the ImageCapture Class to this Game Object
        gameObject.AddComponent<ImageCapture>();

        Instantiate(TipsPanel, new Vector3(0, 0, 0), new Quaternion(0, 0, 1.6f, 0));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Instantiate a Label in the appropriate location relative to the Main Camera.
    /// </summary>
    private void CreateLabel3d()
    {
        lastLabelPlaced = Instantiate(labelPrefab, cursor.transform.position, new Quaternion(0, transform.rotation.y-0.5f, transform.rotation.z, transform.rotation.w));

        lastLabelPlacedText = lastLabelPlaced.GetComponent<TextMesh>();

        // Change the text of the label to show that has been placed
        // The final text will be set at a later stage
        lastLabelPlacedText.text = "Analysing...";
    }

    private void SetTagsToLastLabel(string tag)
    {
        lastLabelPlacedText = lastLabelPlaced.GetComponent<TextMesh>();

        if (tag != null)
        {
            // At this point we go through all the tags received and set them as text of the label
            lastLabelPlacedText.text = "This is " + tag;
        }
    }

    /// <summary>
    /// Detect faces from a submitted image
    /// </summary>
    internal IEnumerator DetectFacesFromImage()
    {
        WWWForm webForm = new WWWForm();
        string detectFacesEndpoint = $"{baseEndpoint}detect";

        // Change the image into a bytes array
        imageBytes = GetImageAsByteArray(imagePath);

        using (UnityWebRequest www =
            UnityWebRequest.Post(detectFacesEndpoint, webForm))
        {
            www.SetRequestHeader("Ocp-Apim-Subscription-Key", key);
            www.SetRequestHeader("Content-Type", "application/octet-stream");
            www.uploadHandler.contentType = "application/octet-stream";
            www.uploadHandler = new UploadHandlerRaw(imageBytes);
            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.SendWebRequest();
            string jsonResponse = www.downloadHandler.text;
            Face_RootObject[] face_RootObject =
                JsonConvert.DeserializeObject<Face_RootObject[]>(jsonResponse);

            List<string> facesIdList = new List<string>();
            // Create a list with the face Ids of faces detected in image
            foreach (Face_RootObject faceRO in face_RootObject)
            {
                facesIdList.Add(faceRO.faceId);
                Debug.Log($"Detected face - Id: {faceRO.faceId}");
            }

            StartCoroutine(IdentifyFaces(facesIdList));
        }
    }

    /// <summary>
    /// Identify the faces found in the image within the person group
    /// </summary>
    internal IEnumerator IdentifyFaces(List<string> listOfFacesIdToIdentify)
    {
        CreateLabel3d();
        // Create the object hosting the faces to identify
        FacesToIdentify_RootObject facesToIdentify = new FacesToIdentify_RootObject();
        facesToIdentify.faceIds = new List<string>();
        facesToIdentify.personGroupId = personGroupId;
        foreach (string facesId in listOfFacesIdToIdentify)
        {
            facesToIdentify.faceIds.Add(facesId);
        }
        facesToIdentify.maxNumOfCandidatesReturned = 1;
        facesToIdentify.confidenceThreshold = 0.5;

        // Serialise to Json format
        string facesToIdentifyJson = JsonConvert.SerializeObject(facesToIdentify);
        // Change the object into a bytes array
        byte[] facesData = Encoding.UTF8.GetBytes(facesToIdentifyJson);

        WWWForm webForm = new WWWForm();
        string detectFacesEndpoint = $"{baseEndpoint}identify";

        using (UnityWebRequest www = UnityWebRequest.Post(detectFacesEndpoint, webForm))
        {
            www.SetRequestHeader("Ocp-Apim-Subscription-Key", key);
            www.SetRequestHeader("Content-Type", "application/json");
            www.uploadHandler.contentType = "application/json";
            www.uploadHandler = new UploadHandlerRaw(facesData);
            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.SendWebRequest();
            string jsonResponse = www.downloadHandler.text;
            Debug.Log($"Get Person - jsonResponse: {jsonResponse}");
            try
            {

                Candidate_RootObject[] candidate_RootObject = JsonConvert.DeserializeObject<Candidate_RootObject[]>(jsonResponse);

                // For each face to identify that ahs been submitted, display its candidate
                foreach (Candidate_RootObject candidateRO in candidate_RootObject)
                {
                    try
                    {
                        StartCoroutine(GetPerson(candidateRO.candidates[0].personId));
                    }
                    catch
                    {
                        lastLabelPlacedText.text = 
                            "Person doesn't recognized, please make \n" +
                            "sure that this person is registered in\n" +
                            "the showcase's face API resource.";
                    }
                    // Delay the next "GetPerson" call, so all faces candidate are displayed properly
                }
            }
            catch
            {
                lastLabelPlacedText.text = 
                    "Please get focus\n" +
                    "in a person and make\n" +
                    "Air Tap gesture";
            }
            yield return new WaitForSeconds(3);
        }
    }

    /// <summary>
    /// Provided a personId, retrieve the person name associated with it
    /// </summary>
    internal IEnumerator GetPerson(string personId)
    {
        string getGroupEndpoint = $"{baseEndpoint}persongroups/{personGroupId}/persons/{personId}?";
        WWWForm webForm = new WWWForm();

        using (UnityWebRequest www = UnityWebRequest.Get(getGroupEndpoint))
        {
            www.SetRequestHeader("Ocp-Apim-Subscription-Key", key);
            www.downloadHandler = new DownloadHandlerBuffer();
            yield return www.SendWebRequest();
            string jsonResponse = www.downloadHandler.text;

            Debug.Log($"Get Person - jsonResponse: {jsonResponse}");
            IdentifiedPerson_RootObject identifiedPerson_RootObject = JsonConvert.DeserializeObject<IdentifiedPerson_RootObject>(jsonResponse);

            SetTagsToLastLabel(identifiedPerson_RootObject.name);
        }
    }

    /// <summary>
    /// Returns the contents of the specified file as a byte array.
    /// </summary>
    static byte[] GetImageAsByteArray(string imageFilePath)
    {
        FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
        BinaryReader binaryReader = new BinaryReader(fileStream);
        return binaryReader.ReadBytes((int)fileStream.Length);
    }
    #endregion
}

/// <summary>
/// The Person Group object
/// </summary>
public class Group_RootObject
{
    public string personGroupId { get; set; }
    public string name { get; set; }
    public object userData { get; set; }
}

/// <summary>
/// The Person Face object
/// </summary>
public class Face_RootObject
{
    public string faceId { get; set; }
}

/// <summary>
/// Collection of faces that needs to be identified
/// </summary>
public class FacesToIdentify_RootObject
{
    public string personGroupId { get; set; }
    public List<string> faceIds { get; set; }
    public int maxNumOfCandidatesReturned { get; set; }
    public double confidenceThreshold { get; set; }
}

/// <summary>
/// Collection of Candidates for the face
/// </summary>
public class Candidate_RootObject
{
    public string faceId { get; set; }
    public List<Candidate> candidates { get; set; }
}

public class Candidate
{
    public string personId { get; set; }
    public double confidence { get; set; }
}

/// <summary>
/// Name and Id of the identified Person
/// </summary>
public class IdentifiedPerson_RootObject
{
    public string personId { get; set; }
    public string name { get; set; }
}
