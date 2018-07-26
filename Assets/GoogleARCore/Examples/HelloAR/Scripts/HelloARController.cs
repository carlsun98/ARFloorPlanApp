//-----------------------------------------------------------------------
// <copyright file="HelloARController.cs" company="Google">
//
// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace GoogleARCore.Examples.HelloAR
{
    using System;
    using System.Collections.Generic;
    using GoogleARCore;
    using GoogleARCore.Examples.Common;
    using UnityEngine;

#if UNITY_EDITOR
    // Set up touch input propagation while using Instant Preview in the editor.
    using Input = InstantPreviewInput;
#endif

    /// <summary>
    /// Controls the HelloAR example.
    /// </summary>
    public class HelloARController : MonoBehaviour
    {
        /// <summary>
        /// The first-person camera being used to render the passthrough camera image (i.e. AR background).
        /// </summary>
        public Camera FirstPersonCamera;

        /// <summary>
        /// A prefab for tracking and visualizing detected planes.
        /// </summary>
        public GameObject DetectedPlanePrefab;

        /// <summary>
        /// A model to place when a raycast from a user touch hits a plane.
        /// </summary>
        public GameObject AndyAndroidPrefab;

        // This GameObject is similar to the one above, except in the Unity editor,
        // this one is displayed by an overhead camera onto the Minimap Render Texture.
        // Thus, it doesn't show up in the real world space.
        public GameObject FlatAndyAndroidPrefab;

        // This GameObject was supposed to be a player sphere (found in the Assets -> GoogleARCore -> Examples -> Common -> Prefabs).
        // It has a minimap camera attached to it that also renders to the Minimap Render Texture. The goal is to have camera follow
        // the player sphere as the user moves around in real time. This needs to be implemented.

        // public GameObject PlayerPrefab;

        /// <summary>
        /// A gameobject parenting UI for displaying the "searching for planes" snackbar.
        /// </summary>
        public GameObject SearchingForPlaneUI;

        /// <summary>
        /// The rotation in degrees need to apply to model when the Andy model is placed.
        /// </summary>
        private const float k_ModelRotation = 180.0f;

        /// <summary>
        /// A list to hold all planes ARCore is tracking in the current frame. This object is used across
        /// the application to avoid per-frame allocations.
        /// </summary>
        private List<DetectedPlane> m_AllPlanes = new List<DetectedPlane>();

        /// <summary>
        /// True if the app is in the process of quitting due to an ARCore connection error, otherwise false.
        /// </summary>
        private bool m_IsQuitting = false;


        // The following three Lists are Lists of GameObjects. Points stores the instances of AndyAndroidPrefab, which are the spheres the user sees
        // when they tap on a generated plane.
        public List<GameObject> Points = new List<GameObject>();

        // flatPoints stores instances of FlatAndyAndroidPrefab, which are the spheres that show up on the Canvas in the bottom left (minimap).
        public List<GameObject> flatPoints = new List<GameObject>();

        // playerLocations was supposed to store the instances of the player's sphere as it moves around and updates. The idea is that
        // every time the Update method gets called, the user's new location would generate a new sphere and get added to the list.
        // This is highly inefficient, so a better solution/any solution is necessary.
        // public List<GameObject> playerLocations = new List<GameObject>();

        // This GameObject shows the distances between two points. The distance (in cm) is displayed above the line between two points on the generated plane.
        public GameObject Text;

        // This GameObject was supposed to take in a similar text object to the one above, except it would display only on the Minimap. Unsuccessfully implemented,
        // needs a better solution.
        // public GameObject flatText;

        /// <summary>
        /// The Unity Update() method.
        /// </summary>
        ///
        public void Update()
        {
            // Don't worry about this method
            _UpdateApplicationLifecycle();

            // Hide snackbar when currently tracking at least one plane. 
            // The "snackbar" is the UI grey bar that shows up with the text "Searching for planes..." at the bottom of your phone screen.
            Session.GetTrackables<DetectedPlane>(m_AllPlanes);
            bool showSearchingUI = true;
            for (int i = 0; i < m_AllPlanes.Count; i++)
            {
                // If you search the list of planes and at least one of their tracking states is "tracked" (meaning the plane has been generated),
                // set showSearchingUI to false so that it doesn't show the snackbar.
                if (m_AllPlanes[i].TrackingState == TrackingState.Tracking)
                {
                    showSearchingUI = false;
                    break;
                }
            }

            SearchingForPlaneUI.SetActive(showSearchingUI);

            // If the player has not touched the screen, we are done with this update.
            Touch touch;
            if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
            {
                return;
            }


            // Raycast against the location the player touched to search for planes.
            TrackableHit hit;
            TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
                TrackableHitFlags.FeaturePointWithSurfaceNormal;

            // This if statement takes in the x and y positions of the touch, as well as the filter, and outputs the result
            // to the variable hit.
            if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit))
            {
                // Use hit pose and camera pose to check if hittest is from the
                // back of the plane, if it is, no need to create the anchor.
                if ((hit.Trackable is DetectedPlane) &&
                    Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
                        hit.Pose.rotation * Vector3.up) < 0)
                {
                    Debug.Log("Hit at back of the current DetectedPlane");
                }

                // The following else if structure is for when the user taps on/near the first point they made.
                // This checks that A. there are at least 3 points out on the plane (triangle is the lowest side polygon) and
                // B. the absolute difference between the tapped position and the first point's position is less than 0.1 (in Unity coordinates I think).
                // B is checked in the x, y, and z directions. The constant 0.1 was found through guess and check,
                // but if the developer wishes to increase the margin of detection, they can increase it from 0.1.
                else if (Points.Count > 2 && Math.Abs(hit.Pose.position.x - Points[0].transform.position.x) < 0.1 && Math.Abs(hit.Pose.position.y - Points[0].transform.position.y) < 0.1 && Math.Abs(hit.Pose.position.z - Points[0].transform.position.z) < 0.1)
                {
                    // This block of code sets the first point's line renderer to render between two positions. 
                    // Then it sets the line renderer's first position to be the first point's position
                    // and the second position to be the most recently created point's position. Thus, drawing a line between the first and last points.
                    Points[0].GetComponent<LineRenderer>().positionCount = 2;
                    Points[0].GetComponent<LineRenderer>().SetPosition(0, Points[0].transform.position);
                    Points[0].GetComponent<LineRenderer>().SetPosition(1, Points[Points.Count - 1].transform.position);

                    // Instantiate creates an instance of the first argument (Text), and places it at a certain position and rotation (second and third arguments).
                    // Quaternion.identity is the identity rotation (like the identity matrix from linear algebra). It's the default.
                    var temp = Instantiate(Text, (Points[0].transform.position + Points[Points.Count - 1].transform.position) / 2, Quaternion.identity);

                    // Rotates the text so that it faces the line.
                    temp.transform.LookAt(Points[0].transform.position);
                    temp.transform.localEulerAngles = new Vector3(90, temp.transform.localEulerAngles.y + 90, 0);

                    // Sets the text component of Text to be the distance in centimeters (hence the * 100) and rounds to two decimal places.
                    temp.GetComponent<TextMesh>().text = (Vector3.Distance(Points[0].transform.position, Points[Points.Count - 1].transform.position) * 100).ToString("0.00");

                    // The following blocks of code are similar to the previous blocks, except for a Text object that only displayed on the Minimap layer.
                    // It didn't work, so I'm commenting it out for now.
                    /*flatPoints[0].GetComponent<LineRenderer>().positionCount = 2;
                    flatPoints[0].GetComponent<LineRenderer>().SetPosition(0, flatPoints[0].transform.position);
                    flatPoints[0].GetComponent<LineRenderer>().SetPosition(1, flatPoints[flatPoints.Count - 1].transform.position);

                    temp = Instantiate(flatText, (flatPoints[0].transform.position + flatPoints[flatPoints.Count - 1].transform.position) / 2, Quaternion.identity);
                    temp.transform.LookAt(flatPoints[0].transform.position);
                    temp.transform.localEulerAngles = new Vector3(90, temp.transform.localEulerAngles.y + 90, 0);

                    temp.GetComponent<TextMesh>().text = (Vector3.Distance(flatPoints[0].transform.position, flatPoints[flatPoints.Count - 1].transform.position) * 100).ToString("0.00");
                    */
                }

                // This else statement does the regular sphere placement, when the user isn't connecting back to the first point.
                else
                {
                    // Instantiate Andy model at the hit pose.
                    var andyObject = Instantiate(AndyAndroidPrefab, hit.Pose.position, hit.Pose.rotation);

                    // Instantiate the minimap Andy model at the hit pose, but it won't display since it's on the Minimap layer.
                    var flatAndyObject = Instantiate(FlatAndyAndroidPrefab, hit.Pose.position, hit.Pose.rotation);

                    // This code is meant to instantiate a player object at the camera/user's location, but it didn't work.
                    // Commented out
                    // var playerObject = Instantiate(PlayerPrefab, FirstPersonCamera.transform.position, FirstPersonCamera.transform.rotation);

                    // Add the points to their lists to keep track of them.
                    Points.Add(andyObject);
                    flatPoints.Add(flatAndyObject);
                    // playerLocations.Add(playerObject);

                    // If there are 2 or more points in the list, draw the line between the two most recent points. Display their distance.
                    if (Points.Count >= 2)
                    {
                        // Does similar things to the code in the else if block above. The first point is the point just placed, 
                        // and the second point is the second to last point in the list (since we added the point just placed to the end of the list).
                        andyObject.GetComponent<LineRenderer>().positionCount = 2;
                        andyObject.GetComponent<LineRenderer>().SetPosition(0, andyObject.transform.position);
                        andyObject.GetComponent<LineRenderer>().SetPosition(1, Points[Points.Count - 2].transform.position);

                        var temp = Instantiate(Text, (andyObject.transform.position + Points[Points.Count - 2].transform.position) / 2, Quaternion.identity);
                        temp.transform.LookAt(andyObject.transform.position);
                        temp.transform.localEulerAngles = new Vector3(90, temp.transform.localEulerAngles.y + 90, 0);

                        temp.GetComponent<TextMesh>().text = (Vector3.Distance(andyObject.transform.position, Points[Points.Count - 2].transform.position) * 100).ToString("0.00");

                        flatAndyObject.GetComponent<LineRenderer>().positionCount = 2;
                        flatAndyObject.GetComponent<LineRenderer>().SetPosition(0, flatAndyObject.transform.position);
                        flatAndyObject.GetComponent<LineRenderer>().SetPosition(1, flatPoints[flatPoints.Count - 2].transform.position);

                        // This block of code was supposed to instantiate flat text onto the Minimap layer, but it didn't work.
                        /*temp = Instantiate(flatText, (flatAndyObject.transform.position + flatPoints[flatPoints.Count - 2].transform.position) / 2, Quaternion.identity);
                        temp.transform.LookAt(flatAndyObject.transform.position);
                        temp.transform.localEulerAngles = new Vector3(90, temp.transform.localEulerAngles.y + 90, 0);
                        
                        temp.GetComponent<TextMesh>().text = (Vector3.Distance(flatAndyObject.transform.position, flatPoints[flatPoints.Count - 2].transform.position) * 100).ToString("0.00");
                        */
                    }

                    // Compensate for the hitPose rotation facing away from the raycast (i.e. camera).
                    // Rotates the objects back 180 degrees so that it faces the camera. This doesn't really matter since it's a sphere, and is kept
                    // from the original sample project's code, which used Andy Android objects rather than spheres.
                    andyObject.transform.Rotate(0, k_ModelRotation, 0, Space.Self);

                    flatAndyObject.transform.Rotate(0, k_ModelRotation, 0, Space.Self);

                    // Create an anchor to allow ARCore to track the hitpoint as understanding of the physical
                    // world evolves.
                    var anchor = hit.Trackable.CreateAnchor(hit.Pose);

                    // Make Andy model a child of the anchor.
                    andyObject.transform.parent = anchor.transform;

                    flatAndyObject.transform.parent = anchor.transform;
                }
            }
        }

        /// <summary>
        /// Check and update the application lifecycle.
        /// </summary>
        private void _UpdateApplicationLifecycle()
        {
            // Exit the app when the 'back' button is pressed.
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            // Only allow the screen to sleep when not tracking.
            if (Session.Status != SessionStatus.Tracking)
            {
                const int lostTrackingSleepTimeout = 15;
                Screen.sleepTimeout = lostTrackingSleepTimeout;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (m_IsQuitting)
            {
                return;
            }

            // Quit if ARCore was unable to connect and give Unity some time for the toast to appear.
            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                _ShowAndroidToastMessage("Camera permission is needed to run this application.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status.IsError())
            {
                _ShowAndroidToastMessage("ARCore encountered a problem connecting.  Please start the app again.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        /// <summary>
        /// Actually quit the application.
        /// </summary>
        private void _DoQuit()
        {
            Application.Quit();
        }

        /// <summary>
        /// Show an Android toast message.
        /// </summary>
        /// <param name="message">Message string to show in the toast.</param>
        private void _ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                        message, 0);
                    toastObject.Call("show");
                }));
            }
        }
    }
}
