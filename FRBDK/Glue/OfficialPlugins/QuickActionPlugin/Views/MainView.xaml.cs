﻿using FlatRedBall.Glue.Elements;
using FlatRedBall.Glue.Plugins;
using FlatRedBall.Glue.Plugins.ExportedImplementations;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.Glue.SetVariable;
using FlatRedBall.Glue.ViewModels;
using GlueFormsCore.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FileManager = ToolsUtilities.FileManager;

namespace OfficialPlugins.QuickActionPlugin.Views
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : UserControl
    {
        const string GameScreenName = "Screens\\GameScreen";

        public event Action AnyButtonClicked;

        public MainView()
        {
            InitializeComponent();
        }

        #region Add Screen

        private void AddScreenButton_Clicked(object sender, RoutedEventArgs e)
        {
            GlueCommands.Self.DialogCommands.ShowAddNewScreenDialog();
            AnyButtonClicked();
        }

        #endregion

        #region Open Project

        private void OpenProjectButton_Clicked(object sender, RoutedEventArgs e)
        {
            GlueCommands.Self.DialogCommands.ShowLoadProjectDialog();
            AnyButtonClicked();
        }

        #endregion

        private void AddEntityButton_Clicked(object sender, RoutedEventArgs e)
        {
            GlueCommands.Self.DialogCommands.ShowAddNewEntityDialog();
            AnyButtonClicked();
        }

        private void RunWizard_Clicked(object sender, RoutedEventArgs e)
        {
            PluginManager.CallPluginMethod(Localization.Texts.ProjectWizardNew, "RunWizard");
        }

        private void CreateNewProjectButton_Clicked(object sender, RoutedEventArgs e)
        {
            GlueCommands.Self.ProjectCommands.CreateNewProject();
            AnyButtonClicked();
        }

        private void AddGumButton_Clicked(object sender, RoutedEventArgs args)
        {
            PluginManager.CallPluginMethod(Localization.Texts.GumPlugin, "AskToCreateGumProject");
        }

        #region Add Object

        private void AddObjectButton_Clicked(object sender, RoutedEventArgs e)
        {
            // before deselecting the object, store off the element
            var element = GlueState.Self.CurrentElement;
            // deselect the currently selected named object
            if (GlueState.Self.CurrentNamedObjectSave != null)
            {
                GlueState.Self.CurrentNamedObjectSave = null;

                //re-select the element since deselecting the NOS will deselect everything
                GlueState.Self.CurrentElement = element;
            }
            if(GlueState.Self.CurrentElement != null)
            {
                GlueCommands.Self.DialogCommands.ShowAddNewObjectDialog();
                AnyButtonClicked();
            }
        }

        #endregion

        private async void AddInstanceOfEntityButton_Clicked(object sender, RoutedEventArgs e)
        {
            var gameScreen = GlueState.Self.CurrentGlueProject.GetScreenSave(GameScreenName);
            var viewModel = new AddObjectViewModel();
            viewModel.ForcedElementToAddTo = gameScreen;
            viewModel.SourceType = SourceType.Entity;

            viewModel.SelectedEntitySave = GlueState.Self.CurrentEntitySave;
            viewModel.ObjectName = GlueState.Self.CurrentEntitySave.GetStrippedName() + "1";


            var listOfThisType = ObjectFinder.Self.GetDefaultListToContain(GlueState.Self.CurrentEntitySave, gameScreen);

            var newNos = await GlueCommands.Self.GluxCommands.AddNewNamedObjectToAsync(viewModel, gameScreen, listOfThisType);

            GlueState.Self.CurrentNamedObjectSave = newNos;

            AnyButtonClicked();
        }

        private async void AddListOfEntityButton_Clicked(object sender, RoutedEventArgs e)
        {
            var gameScreen = GlueState.Self.CurrentGlueProject.GetScreenSave(GameScreenName);
            var viewModel = new AddObjectViewModel();
            viewModel.ForcedElementToAddTo = gameScreen;
            viewModel.SourceType = SourceType.FlatRedBallType;

            viewModel.SelectedAti = AvailableAssetTypes.CommonAtis.PositionedObjectList;
            viewModel.SourceClassGenericType = GlueState.Self.CurrentEntitySave.Name;
            viewModel.ObjectName = GlueState.Self.CurrentEntitySave.GetStrippedName() + "List";

            var newNos = await GlueCommands.Self.GluxCommands.AddNewNamedObjectToAsync(viewModel, gameScreen, null);

            newNos.ExposedInDerived = true;
            InheritanceManager.UpdateAllDerivedElementFromBaseValues(regenerateCode:false, gameScreen);
            AnyButtonClicked();
        }

        private void AddEntityFactory_Clicked(object sender, RoutedEventArgs e)
        {
            var entity = GlueState.Self.CurrentEntitySave;

            entity.CreatedByOtherEntities = true;

            EditorObjects.IoC.Container.Get<SetPropertyManager>().ReactToPropertyChanged(
                nameof(entity.CreatedByOtherEntities), false, nameof(entity.CreatedByOtherEntities), null);

            GlueCommands.Self.GluxCommands.SaveProjectAndElements();

            AnyButtonClicked();
        }

        private async void AddObjectToListButton_Clicked(object sender, RoutedEventArgs e)
        {
            var namedObject = new NamedObjectSave();
            namedObject.SetDefaults();
            var targetList = GlueState.Self.CurrentNamedObjectSave;
            var targetElement = GlueState.Self.CurrentElement;
            if(!targetList.IsList)
            {
                targetList = GlueState.Self.CurrentElement.NamedObjects
                    .FirstOrDefault(item => item.ContainedObjects.Contains(GlueState.Self.CurrentNamedObjectSave));
            }

            //////////////////Early Out//////////////////
            if(targetList == null)
            {
                return;
            }
            ////////////////End Early Out////////////////



            var desiredType = targetList?.SourceClassGenericType;

            namedObject.InstanceName =
                FileManager.RemovePath(desiredType) + "1";

            FlatRedBall.Utilities.StringFunctions.MakeNameUnique<NamedObjectSave>(
                namedObject, targetList.ContainedObjects);

            // Not sure if we need to set this or not, but I think 
            // any instance added to a list will not be defined by base
            namedObject.DefinedByBase = false;

            NamedObjectSaveExtensionMethodsGlue.AddNamedObjectToList(namedObject,
                targetList);

            if (namedObject.SourceClassType != desiredType)
            {
                namedObject.SourceClassType = desiredType;
                namedObject.UpdateCustomProperties();
            }

            GlueCommands.Self.GenerateCodeCommands.GenerateElementCode(targetElement);
            GlueCommands.Self.RefreshCommands.RefreshTreeNodeFor(targetElement);
            //PluginManager.ReactToNewObject(namedObject);
            await PluginManager.ReactToNewObjectListAsync(new List<NamedObjectSave>() { namedObject });

            if (targetList != null)
            {
                await PluginManager.ReactToObjectContainerChanged(namedObject, targetList);
            }

            GlueState.Self.CurrentNamedObjectSave = namedObject;

            await GlueCommands.Self.GluxCommands.SaveElementAsync(targetElement);

            AnyButtonClicked();
        }
    }
}
