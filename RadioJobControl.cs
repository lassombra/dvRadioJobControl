using CommandTerminal;
using DV;
using DV.Booklets;
using DV.Logic.Job;
using DV.ThingTypes;
using DV.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace dvRadioJobControl
{
    /**
     * stolen from SkinManager
     */
    public class RadioJobControl: MonoBehaviour, ICommsRadioMode
    {
        public static CommsRadioController Controller;

        public ButtonBehaviourType ButtonBehaviour { get; private set; }

        public CommsRadioDisplay display;
        public Transform signalOrigin;
        public Material selectionMaterial;
        public Material skinningMaterial;
        public GameObject trainHighlighter;

        public AudioClip selectedCarSound;
        public AudioClip removeCarSound;
        public AudioClip confirmSound;
        public AudioClip cancelSound;
        public AudioClip warningSound;

        private Act selectedAction;
        private State currentState;

        private RaycastHit Hit;
        private LayerMask TrainCarMask;
        private TrainCar PointedCar = null;
        private MeshRenderer HighlighterRender;

        private static bool PersistentJobsInstalled = false;
        private static readonly Vector3 HIGHLIGHT_BOUNDS_EXTENSION = new Vector3(0.25f, 0.8f, 0f);
        private const float SIGNAL_RANGE = 100f;
        public void OverrideSignalOrigin(Transform signalOrigin) => this.signalOrigin = signalOrigin;

        public Color GetLaserBeamColor()
        {
            return Color.gray;
        }

        #region Initialization

        public void Awake()
        {
            // steal components from other radio modes
            CommsRadioCarDeleter deleter = Controller.deleteControl;

            if (deleter != null)
            {
                signalOrigin = deleter.signalOrigin;
                display = deleter.display;
                selectionMaterial = new Material(deleter.selectionMaterial);
                skinningMaterial = new Material(deleter.deleteMaterial);
                trainHighlighter = deleter.trainHighlighter;

                selectedCarSound = deleter.selectedCarSound;
                removeCarSound = deleter.removeCarSound;
                confirmSound = deleter.confirmSound;
                cancelSound = deleter.cancelSound;
                warningSound = deleter.warningSound;
            }

            PersistentJobsInstalled = UnityModManager.modEntries.Find(modEntry => modEntry.Info.Id == "PersistentJobsMod") != null;
        }

        public void Start()
        {
            TrainCarMask = LayerMask.GetMask(new string[] { "Train_Big_Collider" });

            HighlighterRender = trainHighlighter.GetComponentInChildren<MeshRenderer>(true);
            trainHighlighter.SetActive(false);
            trainHighlighter.transform.SetParent(null);
        }

        public void Enable() { }

        public void Disable()
        {
            ResetState();
        }

        public void SetStartingDisplay() {
            display.SetDisplay("JOB CONTROL", "manage jobs remotely", "select car");
        }

        #endregion

        #region State Machine Actions

        private void SetState(State newState)
        {
            if (newState == currentState) return;

            currentState = newState;
            switch (newState)
            {
                case State.Entry:
                    SetStartingDisplay();
                    ButtonBehaviour = ButtonBehaviourType.Regular;
                    break;
                case State.SelectCar:
                    ButtonBehaviour = ButtonBehaviourType.Override;
                    break;
            }
        }


        private void ResetState()
        {
            PointedCar = null;
            ClearHighlightedCar();

            SetState(State.Entry);
        }
        private static StationController GetNearestStation(Vector3 position) {
            return StationController.allStations.OrderBy(sc => (position - sc.gameObject.transform.position).sqrMagnitude).First();
        }

        public void OnUse()
        {
            switch (currentState)
            {
                case State.Entry:
                    if (PointedCar != null) {
                        CommsRadioController.PlayAudioFromRadio(selectedCarSound, transform);
                        SetState(State.SelectCar);
                        Job jobOfCar = SingletonBehaviour<JobsManager>.Instance.GetJobOfCar(PointedCar);
                        if (jobOfCar == null) {
                            selectedAction = PersistentJobsInstalled ? Act.reassign : Act.exit;
                        } else {
                            selectedAction = (jobOfCar.State == JobState.Available) ? Act.accept : Act.complete;
                        }
                    }
                    UpdateDisplay();
                    break;
                case State.SelectCar:
                    if (PointedCar != null) {
                        Job jobOfCar = SingletonBehaviour<JobsManager>.Instance.GetJobOfCar(PointedCar);
                        
                        switch (selectedAction) {
                            case Act.accept:
                                if (jobOfCar != null && jobOfCar.State == JobState.Available) {
                                    CommsRadioController.PlayAudioFromRadio(confirmSound, transform);
                                    //SingletonBehaviour<JobsManager>.Instance.TakeJob(jobOfCar, true);

                                    StationController startingStation = GetNearestStation(PointedCar.gameObject.transform.position);
                                    PointOnPlane bookletSpawnSurface = (PointOnPlane)(typeof(StationController).GetField("jobBookletSpawnSurface", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(startingStation));
                                    (Vector3, Quaternion) valueTuple = bookletSpawnSurface.GetRandomPointWithRotationOnPlane();

                                    List<JobOverview> spawnedJobOverviews = (List<JobOverview>)typeof(StationController).GetField("spawnedJobOverviews", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(startingStation);
                                    JobOverview jobOverview = spawnedJobOverviews.Find(jo => jo.job == jobOfCar);
                                    startingStation.TakeJobFromStation(jobOverview);

                                    BookletCreator.CreateJobBooklet(jobOfCar, valueTuple.Item1, valueTuple.Item2, (Transform)null);
                                } else
                                    CommsRadioController.PlayAudioFromRadio(cancelSound, transform);
                                break;

                            case Act.reassign:
                                if (jobOfCar == null) {
                                    if (PersistentJobsInstalled) {
                                        CommsRadioController.PlayAudioFromRadio(confirmSound, transform);
                                        TryReassignPersistentJobs();
                                    } else
                                        CommsRadioController.PlayAudioFromRadio(warningSound, transform);
                                } else
                                    CommsRadioController.PlayAudioFromRadio(cancelSound, transform);
                                break;

                            case Act.complete:
                                if (jobOfCar != null && SingletonBehaviour<JobsManager>.Instance.TryToCompleteAJob(jobOfCar) == JobState.Completed) 
                                    CommsRadioController.PlayAudioFromRadio(confirmSound, transform);
                                else
                                    CommsRadioController.PlayAudioFromRadio(warningSound, transform);
                                
                                break;

                            case Act.discard:
                                if (jobOfCar != null) {
                                    CommsRadioController.PlayAudioFromRadio(confirmSound, transform);
                                    switch (jobOfCar.State)
                                    {
                                        case JobState.Available:
                                            jobOfCar.ExpireJob();
                                            break;
                                        case JobState.InProgress:
                                            SingletonBehaviour<JobsManager>.Instance.AbandonJob(jobOfCar);
                                            break;
                                    }
                                } else
                                    CommsRadioController.PlayAudioFromRadio(cancelSound, transform);
                                break;

                            case Act.exit:
                            default:
                                ResetState();
                                break;

                        }
                    }
                    ResetState();
                    break;
            }
        }


        private void TryReassignPersistentJobs()
        {
            CommandArg carId = new CommandArg() { String = PointedCar.ID.ToString() };
            PersistentJobsMod.Console.RegenerateJobsForConsistOfCar(new CommandArg[] { carId });
        }


        private void UpdateDisplay()
        {
            string displayText = "manage jobs remotely";
            if (PointedCar != null) {
                displayText = "Car: " + PointedCar.ID.ToString();
                Job jobOfCar = SingletonBehaviour<JobsManager>.Instance.GetJobOfCar(PointedCar);
                if (jobOfCar != null) {
                    displayText += "\nJob: " + jobOfCar.ID.ToString()
                        + "\nState: " + jobOfCar.State.ToString();
                }
            }
            switch (currentState) {
                case State.Entry:
                    display.SetDisplay("JOB CONTROL", displayText, PointedCar != null ? PointedCar.ID.ToString() : "select car");
                    break;

                case State.SelectCar:
                    if (PointedCar == null) {
                        display.SetDisplay("JOB CONTROL", "No car selected", $"{Act.exit}");
                    } else {
                        display.SetDisplay("JOB CONTROL", displayText, $"{selectedAction}");
                    }
                    break;
            }
        }

        private bool IsValidAction(Act action) {
            bool hasJob = PointedCar != null && SingletonBehaviour<JobsManager>.Instance.GetJobOfCar(PointedCar) != null;
            switch (action) {
                case Act.reassign:
                    return !hasJob && PersistentJobsInstalled;

                case Act.accept:
                case Act.discard:
                case Act.complete:
                    return hasJob;
            }
            return true;
        }

        public bool ButtonACustomAction()
        {
            if (selectedAction != Act.complete) {
                selectedAction++;
                while (!IsValidAction(selectedAction)) {
                    selectedAction++;
                    if (selectedAction > Act.complete) {
                        selectedAction = Act.exit;
                    }
                }
            }
            UpdateDisplay();
            return true;
        }

        public bool ButtonBCustomAction()
        {
            if (selectedAction != Act.exit) {
                selectedAction--;
                while (!IsValidAction(selectedAction)) {
                    selectedAction--;
                }
            }
            UpdateDisplay();
            return true;
        }

        #endregion

        #region Car Highlighting

        public void OnUpdate()
        {
            TrainCar trainCar;

            switch (currentState) {
                case State.Entry:
                case State.SelectCar:
                    // Check if not pointing at anything
                    if (!Physics.Raycast(signalOrigin.position, signalOrigin.forward, out Hit, SIGNAL_RANGE, TrainCarMask)) {
                        PointToCar(null);
                    } else {
                        // Try to get the traincar we're pointing at
                        trainCar = TrainCar.Resolve(Hit.transform.root);
                        PointToCar(trainCar);
                    }

                    break;

                default:
                    ResetState();
                    break;
            }
        }
        private void HighlightCar(TrainCar car, Material highlightMaterial)
        {
            if (car == null) {
                Debug.LogError("Highlight car is null. Ignoring request.");
                return;
            }

            HighlighterRender.material = highlightMaterial;

            trainHighlighter.transform.localScale = car.Bounds.size + HIGHLIGHT_BOUNDS_EXTENSION;
            Vector3 b = car.transform.up * (trainHighlighter.transform.localScale.y / 2f);
            Vector3 b2 = car.transform.forward * car.Bounds.center.z;
            Vector3 position = car.transform.position + b + b2;

            trainHighlighter.transform.SetPositionAndRotation(position, car.transform.rotation);
            trainHighlighter.SetActive(true);
            trainHighlighter.transform.SetParent(car.transform, true);
        }

        private void ClearHighlightedCar()
        {
            trainHighlighter.SetActive(false);
            trainHighlighter.transform.SetParent(null);
        }

        private void PointToCar(TrainCar car)
        {
            if (PointedCar != car)
            {
                if (car != null)
                {
                    PointedCar = car;
                    HighlightCar(PointedCar, selectionMaterial);
                }
                else
                {
                    PointedCar = null;
                    ClearHighlightedCar();
                }
                UpdateDisplay();
            }
        }

        #endregion
        protected enum Act
        {
            exit,
            discard,
            reassign,
            accept,
            complete
        }

        private enum State
        {
            Entry,
            SelectCar,
        }
    }
}
