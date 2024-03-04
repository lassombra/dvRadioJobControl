using DV;
using DV.Booklets;
using DV.Logic.Job;
using DV.Printers;
using DV.ServicePenalty;
using DV.ThingTypes;
using DV.Utils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace dvRadioJobControl
{

    [HarmonyPatch(typeof(JobValidator), "ValidateJob")]
    static class JobValidator_ValidateJob_Patch
    {
        static bool completed = false;

        public static bool Prefix(JobBooklet jobBooklet)
        {
            if (jobBooklet != null && jobBooklet.job != null && jobBooklet.job.State == JobState.Completed)
            {
                completed = true;
            }
            return true;
        }

        static void Postfix(JobValidator __instance,
            PrinterController ___bookletPrinter,
            MoneyPrinterJobValidator ___moneyPrinter,
            AudioClip ___jobValidatedSound,
            JobBooklet jobBooklet)
        {
            if (jobBooklet == null || completed == false)
            {
                return;
            }
            completed = false;

            Job job = jobBooklet.job;
            if (job != null)
            {
                if (!SingletonBehaviour<JobsManager>.Instance.currentJobs.Contains(job))
                {
                    DisplayableDebt debt = SingletonBehaviour<JobDebtController>.Instance.LastStagedJobDebt;
                    if (debt != null && !debt.IsStaged)
                        debt.UpdateDebtState();

                    BookletCreator.CreateJobReport(job, debt, ___bookletPrinter.spawnAnchor.position, ___bookletPrinter.spawnAnchor.rotation, (Transform)null);
                    CommsRadioController.PlayAudioFromRadio(___jobValidatedSound, __instance.transform);
                    ___bookletPrinter.Print();
                    ___moneyPrinter.PrintPayment(job);
                }
                else
                    ___bookletPrinter.PlayErrorSound();
            }
            else
                ___bookletPrinter.PlayErrorSound();

            jobBooklet.DestroyJobBooklet();
        }
    }

    [HarmonyPatch(typeof(CommsRadioController), "Awake")]
    static class CommsRadio_Awake_Patch
    {
        public static RadioJobControl jobControl;

        static void Postfix(CommsRadioController __instance, List<ICommsRadioMode> ___allModes)
        {
            RadioJobControl.Controller = __instance;
            jobControl = __instance.gameObject.AddComponent<RadioJobControl>();
            ___allModes.Add(jobControl);
        }
    }
}
