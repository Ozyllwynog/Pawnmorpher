﻿// Pawnmorpher_DebugDialogue.cs modified by Iron Wolf for Pawnmorph on 08/02/2019 7:29 PM
// last updated 08/02/2019  7:29 PM

using System.Collections.Generic;
using System.Linq;
using System.Text;
using AlienRace;
using JetBrains.Annotations;
using Pawnmorph.GraphicSys;
using Pawnmorph.Hediffs;
using Pawnmorph.Hybrids;
using Pawnmorph.Utilities;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

#pragma warning disable 1591
namespace Pawnmorph.DebugUtils
{
    public class Pawnmorpher_DebugDialogue : Dialog_DebugOptionLister
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Pawnmorpher_DebugDialogue" /> class.
        /// </summary>
        public Pawnmorpher_DebugDialogue()
        {
            forcePause = true;
        }

        protected override void DoListingItems()
        {
            if (KeyBindingDefOf.Dev_ToggleDebugActionsMenu.KeyDownEvent)
            {
                Event.current.Use();
                Close();
            }

            if (Current.ProgramState == ProgramState.Playing)
                if (Find.CurrentMap != null)
                    ListPlayOptions();
        }

        private void AddAspectAtStage(AspectDef def, Pawn p, int i)
        {
            p.GetAspectTracker()?.Add(def, i);
        }



        void TryStartRandomHunt(Pawn pawn)
        {
            if (!pawn.RaceProps.predator) return;
            var prey = FormerHumanUtilities.FindRandomPreyFor(pawn);
            if (prey == null) return;
            var job = new Job(JobDefOf.PredatorHunt, prey)
            {
                killIncappedTarget = true
            };

            pawn.jobs?.StartJob(job, JobCondition.InterruptForced);
        }

        

        private void ForceTransformation(Pawn pawn)
        {
            var allHediffs = pawn.health.hediffSet.hediffs;
            if (allHediffs == null) return;

            foreach (Hediff hediff in allHediffs)
            {
                var transformer = hediff.def.GetAllTransformers().FirstOrDefault();
                if (transformer != null)
                {
                    transformer.TransformPawn(pawn, hediff);
                    return; 
                }
            }
        }

        void RecalculateInfluence(Pawn pawn)
        {
            pawn?.GetComp<MutationTracker>()?.RecalculateMutationInfluences();
        }

        void AllRecalculateInfluence()
        {
            foreach (Pawn colonist in PawnsFinder.AllMaps_FreeColonists)
            {
                RecalculateInfluence(colonist); 
            }
        }

        void ResetMutationProgression( Pawn pawn)
        {
            var allHediffs = pawn?.health?.hediffSet?.hediffs;
            if (allHediffs == null) return;

            foreach (Hediff_AddedMutation mutation in allHediffs.OfType<Hediff_AddedMutation>())
            {
                mutation.SeverityAdjust?.Restart();
            }
        }

        List<DebugMenuOption> GetGiveBackstoriesOptions(Pawn pawn)
        {
            List<DebugMenuOption> options = new List<DebugMenuOption>(); 
            foreach (BackstoryDef backstoryDef in DefDatabase<BackstoryDef>.AllDefs)
            {
                var def = backstoryDef; 
                options.Add(new DebugMenuOption(def.defName, DebugMenuOptionMode.Action, () => AddBackstoryToPawn(pawn, def)));
            }

            return options; 
        }

        void AddBackstoryToPawn(Pawn pawn, BackstoryDef def)
        {
            pawn.story.adulthood = def.backstory; 
        }

        private List<DebugMenuOption> GetAddAspectOptions(AspectDef def, Pawn p)
        {
            var outLst = new List<DebugMenuOption>();
            for (var i = 0; i < def.stages.Count; i++)
            {
                AspectStage stage = def.stages[i];
                int i1 = i; //need to make a copy 
                var label = string.IsNullOrEmpty(stage.label) ? def.label : stage.label; 
                outLst.Add(new DebugMenuOption($"{i}) {label}", DebugMenuOptionMode.Action,
                                               () => AddAspectAtStage(def, p, i1)));
            }

            return outLst;
        }

        private List<DebugMenuOption> GetAddAspectOptions(Pawn pawn)
        {
            AspectTracker tracker = pawn.GetAspectTracker();
            var outLst = new List<DebugMenuOption>();
            foreach (AspectDef aspectDef in DefDatabase<AspectDef>.AllDefs.Where(d => !tracker.Contains(d))
            ) //don't allow aspects to be added more then once 
            {
                AspectDef tmpDef = aspectDef;

                outLst.Add(new DebugMenuOption($"{aspectDef.defName}", DebugMenuOptionMode.Action,
                                               () =>
                                                   Find.WindowStack
                                                       .Add(new Dialog_DebugOptionListLister(GetAddAspectOptions(tmpDef,
                                                                                                                 pawn)))));
            }

            return outLst;
        }


        private List<DebugMenuOption> GetRaceChangeOptions()
        {
            //var races = RaceGenerator.ImplicitRaces;
            var lst = new List<DebugMenuOption>();
            foreach (MorphDef morph in DefDatabase<MorphDef>.AllDefs)
            {
                MorphDef local = morph;

                lst.Add(new DebugMenuOption(local.label, DebugMenuOptionMode.Tool, () =>
                {
                    Pawn pawn = Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Pawn>().FirstOrDefault();
                    if (pawn != null && pawn.RaceProps.intelligence == Intelligence.Humanlike)
                        RaceShiftUtilities.ChangePawnToMorph(pawn, local);
                }));
            }

            return lst;
        }


        private void GetRandomMutationsOptions()
        {
            var options = new List<DebugMenuOption>
                {new DebugMenuOption("none", DebugMenuOptionMode.Tool, () => GivePawnRandomMutations(null))};


            foreach (MorphDef morphDef in MorphDef.AllDefs)
            {
                var option = new DebugMenuOption(morphDef.label, DebugMenuOptionMode.Tool,
                                                 () => GivePawnRandomMutations(morphDef));
                options.Add(option);
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        private List<DebugMenuOption> GetRemoveAspectOptions(Pawn p)
        {
            var outLst = new List<DebugMenuOption>();


            AspectTracker aspectTracker = p.GetAspectTracker();
            if (aspectTracker == null) return outLst;
            foreach (Aspect aspect in aspectTracker.Aspects.ToList())
            {
                Aspect tmpAspect = aspect;
                outLst.Add(new DebugMenuOption($"{aspect.Label}", DebugMenuOptionMode.Action,
                                               () => aspectTracker.Remove(tmpAspect)));
            }

            return outLst;
        }

        void RunRaceCheck(Pawn pawn)
        {
            if (pawn == null) return;
            if (pawn.IsAnimalOrMerged()) return;
            var oldRace = pawn.def;
            pawn.CheckRace();
            if (pawn.def == oldRace)
            {
                Log.Message($"no change in {pawn.Name}");
            }
            else
            {
                Log.Message($"{pawn.Name} was {oldRace.defName} and is now {pawn.def.defName}");
            }
        }


        private void GivePawnRandomMutations([CanBeNull] MorphDef morph)
        {
            Pawn pawn = Find.CurrentMap.thingGrid
                            .ThingsAt(UI.MouseCell())
                            .OfType<Pawn>()
                            .FirstOrDefault();
            if (pawn == null) return;


            var mutations = morph?.AllAssociatedMutations;
            if (mutations == null)
                mutations = DefDatabase<MutationDef>.AllDefs;

            bool CanReceiveGiver(MutationDef mutation)
            {
                IEnumerable<Hediff> hediffs = pawn.health.hediffSet.hediffs.Where(h => h.def == mutation);

                return hediffs.Count() < mutation.parts?.Count;
            }

            var mutList = mutations.Where(CanReceiveGiver).ToList();
            if (mutList.Count == 0) return;

            int num = Rand.Range(1, Mathf.Min(10, mutList.Count));

            var i = 0;
            while (i < num && mutList.Count > 0)
            {
                var giver = mutList.RandElement();
                mutList.Remove(giver);

                MutationUtilities.AddMutation(pawn, giver); 
                
                i++;
            }
        }

        private void ListPawnInitialGraphics(Pawn pawn)
        {
            var initialComp = pawn.GetComp<InitialGraphicsComp>();
            if (initialComp == null) return;

            Log.Message(initialComp.GetDebugStr());
        }

        void DoRemoveAspectsOption(Pawn p)
        {
            var options = GetRemoveAspectOptions(p);
            if (options.Count == 0) return;
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options)); 
        }

        void DoAddAspectToPawn(Pawn p)
        {
            var options = GetAddAspectOptions(p);
            if (options.Count == 0) return;
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options)); 
        }

        void DoAddBackstoryToPawn(Pawn pawn)
        {
            if (!pawn.IsSapientFormerHuman()) return;

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(GetGiveBackstoriesOptions(pawn))); 

        }

        void RunRaceCheck()
        {
            StringBuilder builder = new StringBuilder();
            foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonistsAndPrisonersSpawned)
            {
                RunRaceCheck(pawn); 
            }
        }

        private void ListPlayOptions()
        {
            DebugAction("shift race", () => { Find.WindowStack.Add(new Dialog_DebugOptionListLister(GetRaceChangeOptions())); });
            DebugAction("give random mutations", GetRandomMutationsOptions);
            DebugAction("recalculate all colonist mutation influence", AllRecalculateInfluence);
            DebugAction("run race check on all pawn", RunRaceCheck); 
            DebugToolMapForPawns("force full transformation", ForceTransformation);
            DebugToolMapForPawns("get initial graphics", ListPawnInitialGraphics);
            DebugToolMapForPawns("Remove Aspect", DoRemoveAspectsOption);
            DebugToolMapForPawns("Add Aspect", DoAddAspectToPawn); 
            DebugToolMapForPawns("Add Backstory to Sapient Animal", DoAddBackstoryToPawn);
            DebugToolMapForPawns("Try Random Hunt", TryStartRandomHunt); 
            DebugToolMapForPawns("Make pawn permanently feral", MakePawnPermanentlyFeral);
            DebugToolMapForPawns("Restart all mutation progression", ResetMutationProgression);
            DebugToolMapForPawns("recalculate mutation influence", RecalculateInfluence); 
            DebugToolMapForPawns("Run Race Check", RunRaceCheck); 
        }

        private void MakePawnPermanentlyFeral(Pawn obj)
        {
            if (obj?.IsFormerHuman() != true) return;

            FormerHumanUtilities.MakePermanentlyFeral(obj); 
        }
    }
}