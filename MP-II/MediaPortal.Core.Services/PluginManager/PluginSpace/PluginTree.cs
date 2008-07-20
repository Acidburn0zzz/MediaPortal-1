﻿#region Copyright (C) 2007-2008 Team MediaPortal

/*
 *  Copyright (C) 2007-2008 Team MediaPortal
 *  http://www.team-mediaportal.com
 *
 *  This file is part of MediaPortal II
 *
 *  MediaPortal II is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  MediaPortal II is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 * 
 *  You should have received a copy of the GNU General Public License
 *  along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  Code modified from SharpDevelop AddIn code
 *  Thanks goes to: Mike Krüger
 */

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using MediaPortal.Core;
using MediaPortal.Core.Logging;
using MediaPortal.Core.PluginManager;
using MediaPortal.Services.PluginManager.Builders;
using MediaPortal.Services.PluginManager.PluginDetails;

namespace MediaPortal.Services.PluginManager.PluginSpace
{
  /// <summary>
  ///  class containing the PluginTree. Contains methods for accessing tree nodes and building items.
  /// </summary>
  internal class PluginTree : IPluginTree
  {
    #region Variables

    protected IList<IPluginInfo> _plugins;
    protected PluginTreeNode _rootNode;
    protected IDictionary<string, IPluginBuilder> _builders;

    // do we require conditions?
    //Dictionary<string, IConditionEvaluator> conditionEvaluators = new Dictionary<string, IConditionEvaluator>();

    #endregion

    #region Constructors/Destructors

    public PluginTree()
    {
      // initialise variables
      _plugins = new List<IPluginInfo>();
      _rootNode = new PluginTreeNode();

      // add default builders
      _builders = new Dictionary<string, IPluginBuilder>();
      _builders.Add("Class", new ClassBuilder());

      //conditionEvaluators.Add("Compare", new CompareConditionEvaluator());
      //conditionEvaluators.Add("Ownerstate", new OwnerStateConditionEvaluator());
    }

    #endregion

    #region Public properties

    public IList<IPluginInfo> Plugins
    {
      get { return new List<IPluginInfo>(_plugins); }
    }

    public IDictionary<string, IPluginBuilder> Builders
    {
      get { return _builders; }
    }

    //public Dictionary<string, IConditionEvaluator> ConditionEvaluators
    //{
    //  get
    //  {
    //    return conditionEvaluators;
    //  }
    //}

    #endregion

    #region Public Methods

    public bool IsTreeNode(string path)
    {
      if (path == null || path.Length == 0)
      {
        return true;
      }

      string[] splitPath = path.ToLower().Split('/');
      PluginTreeNode curPath = _rootNode;
      int i = 0;
      while (i < splitPath.Length)
      {
        if (splitPath[i] != string.Empty)
        {
          if (!curPath.ChildNodes.ContainsKey(splitPath[i]))
          {
            return false;
          }
          curPath = curPath.ChildNodes[splitPath[i]];
          ++i;
        }
      }
      return true;
    }

    public PluginTreeNode GetTreeNode(string path)
    {
      return GetTreeNode(path, true);
    }

    public PluginTreeNode GetTreeNode(string path, bool throwOnNotFound)
    {
      if (path == null || path.Length == 0)
      {
        return _rootNode;
      }
      string[] splitPath = path.ToLower().Split('/');
      PluginTreeNode curPath = _rootNode;
      int i = 0;
      while (i < splitPath.Length)
      {
        if (splitPath[i] != string.Empty)
        {
          if (!curPath.ChildNodes.ContainsKey(splitPath[i]))
          {
            if (throwOnNotFound)
              throw new TreePathNotFoundException(path);
            else
              return null;
          }
          curPath = curPath.ChildNodes[splitPath[i]];
        }
        ++i;
      }
      return curPath;
    }

    /// <summary>
    /// Builds a single item in the Plugin tree.
    /// </summary>
    public object BuildItem(string path, object caller)
    {
      int pos = path.LastIndexOf('/');
      string parent = path.Substring(0, pos);
      string child = path.Substring(pos + 1);
      PluginTreeNode node = GetTreeNode(parent);
      return node.BuildChildItem(child, caller, BuildItems(path, caller, false));
    }

    /// <summary>
    /// Builds the items in the path.
    /// </summary>
    /// <param name="path">A path in the Plugin tree.</param>
    /// <param name="caller">The owner used to create the objects.</param>
    /// <param name="throwOnNotFound">If true, throws an TreePathNotFoundException
    /// if the path is not found. If false, an empty ArrayList is returned when the
    /// path is not found.</param>
    public ArrayList BuildItems(string path, object caller, bool throwOnNotFound)
    {
      PluginTreeNode node = GetTreeNode(path, throwOnNotFound);
      if (node == null)
        return new ArrayList();
      else
        return node.BuildChildItems(caller);
    }

    /// <summary>
    /// Builds the items in the path. Ensures that all items have the type T.
    /// Throws an exception if the path is not found.
    /// </summary>
    /// <param name="path">A path in the Plugin tree.</param>
    /// <param name="caller">The owner used to create the objects.</param>
    public List<T> BuildItems<T>(string path, object caller)
    {
      return BuildItems<T>(path, caller, true);
    }

    /// <summary>
    /// Builds the items in the path. Ensures that all items have the type T.
    /// </summary>
    /// <param name="path">A path in the Plugin tree.</param>
    /// <param name="caller">The owner used to create the objects.</param>
    /// <param name="throwOnNotFound">If true, throws an TreePathNotFoundException
    /// if the path is not found. If false, an empty ArrayList is returned when the
    /// path is not found.</param>
    public List<T> BuildItems<T>(string path, object caller, bool throwOnNotFound)
    {
      PluginTreeNode node = GetTreeNode(path, throwOnNotFound);
      if (node == null)
        return new List<T>();
      else
        return node.BuildChildItems<T>(caller);
    }

    /// <summary>
    /// Builds the items in the path. Ensures that all items have the type T.
    /// </summary>
    /// <param name="path">A path in the Plugin tree.</param>
    /// <param name="caller">The owner used to create the objects.</param>
    /// <param name="throwOnNotFound">If true, throws an TreePathNotFoundException
    /// if the path is not found. If false, an empty ArrayList is returned when the
    /// path is not found.</param>
    public object BuildItem<T>(string path, string id, object caller, bool throwOnNotFound)
    {
      PluginTreeNode node = GetTreeNode(path, throwOnNotFound);
      if (node == null)
        return null;
      else
        return node.BuildChildItem<T>(id, caller);
    }

    public void InsertPlugin(PluginInfo Plugin)
    {
      if (Plugin.Enabled)
      {
        foreach (ExtensionPath path in Plugin.ExtensionPaths.Values)
        {
          AddExtensionPath(path);
        }

        foreach (PluginRuntime runtime in Plugin.Runtimes)
        {
          if (runtime.IsActive)
          {
            foreach (LoadBuilder builder in runtime.DefinedBuilders)
            {
              if (_builders.ContainsKey(builder.Name))
              {
                throw new PluginLoadException("Duplicate builder: " + builder.Name);
              }
              _builders.Add(builder.Name, builder);
            }
            //foreach (LazyConditionEvaluator condition in runtime.DefinedConditionEvaluators)
            //{
            //  if (PluginTree.ConditionEvaluators.ContainsKey(condition.Name))
            //  {
            //    throw new PluginLoadException("Duplicate condition evaluator: " + condition.Name);
            //  }
            //  PluginTree.ConditionEvaluators.Add(condition.Name, condition);
            //}
          }
        }

        //string PluginRoot = Path.GetDirectoryName(Plugin.FileName);
        //foreach(string bitmapResource in Plugin.BitmapResources)
        //{
        //  string path = Path.Combine(PluginRoot, bitmapResource);
        //  ResourceManager resourceManager = ResourceManager.CreateFileBasedResourceManager(Path.GetFileNameWithoutExtension(path), Path.GetDirectoryName(path), null);
        //  ResourceService.RegisterNeutralImages(resourceManager);
        //}

        //foreach(string stringResource in Plugin.StringResources)
        //{
        //  string path = Path.Combine(PluginRoot, stringResource);
        //  ResourceManager resourceManager = ResourceManager.CreateFileBasedResourceManager(Path.GetFileNameWithoutExtension(path), Path.GetDirectoryName(path), null);
        //  ResourceService.RegisterNeutralStrings(resourceManager);
        //}
      }
      _plugins.Add(Plugin);
    }

    public void RemovePlugin(PluginInfo Plugin)
    {
      if (Plugin.Enabled)
      {
        throw new ArgumentException("Cannot remove enabled Plugins at runtime.");
      }
      Plugins.Remove(Plugin);
    }

    public void Load(List<string> pluginFiles, List<string> disabledPlugins)
    {
      List<PluginInfo> list = new List<PluginInfo>();
      Dictionary<string, Version> versionDict = new Dictionary<string, Version>();
      Dictionary<string, PluginInfo> pluginInfoDict = new Dictionary<string, PluginInfo>();

      ServiceScope.Get<ILogger>().Debug("Loading Plugin files...");
      foreach (string fileName in pluginFiles)
      {
        ServiceScope.Get<ILogger>().Debug(".Plugin: {0}", fileName);
        PluginInfo plugin;
        try
        {
          plugin = PluginInfo.Load(fileName);
        }
        catch (PluginLoadException ex)
        {
          ServiceScope.Get<ILogger>().Error(ex);
          if (ex.InnerException != null)
          {
            ServiceScope.Get<ILogger>().Error("Error loading Plugin " + fileName + ":\n" + ex.InnerException.Message);
          }
          else
          {
            ServiceScope.Get<ILogger>().Error("Error loading Plugin " + fileName + ":\n" + ex.Message);
          }
          plugin = new PluginInfo();
          //Plugin.CustomErrorMessage = ex.Message;
        }
        //if (Plugin.Action == PluginAction.CustomError)
        //{
        //  list.Add(Plugin);
        //  continue;
        //}
        plugin.Enabled = true;
        if (disabledPlugins != null && disabledPlugins.Count > 0)
        {
          foreach (string name in plugin.Manifest.Identities.Keys)
          {
            if (disabledPlugins.Contains(name))
            {
              plugin.Enabled = false;
              break;
            }
          }
        }
        if (plugin.Enabled)
        {
          foreach (KeyValuePair<string, Version> pair in plugin.Manifest.Identities)
          {
            if (versionDict.ContainsKey(pair.Key))
            {
              //MessageService.ShowError("Name '" + pair.Key + "' is used by " + "'" + PluginDict[pair.Key].FileName + "' and '" + fileName + "'");
              plugin.Enabled = false;
              //Plugin.Action = PluginAction.InstalledTwice;
              break;
            }
            else
            {
              versionDict.Add(pair.Key, pair.Value);
              pluginInfoDict.Add(pair.Key, plugin);
            }
          }
        }
        list.Add(plugin);
      }
      CheckDependencies(list, versionDict, pluginInfoDict);
    	foreach (PluginInfo plugin in list)
      {
        InsertPlugin(plugin);
      }
    }

  	public void CheckDependencies(List<PluginInfo> list, Dictionary<string, Version> versionDict, Dictionary<string, PluginInfo> pluginInfoDict)
  	{
  		checkDependencies:
  		for (int i = 0; i < list.Count; i++)
  		{
  			PluginInfo plugin = list[i];
  			if (!plugin.Enabled) continue;

  			Version versionFound;

  			foreach (PluginReference reference in plugin.Manifest.Conflicts)
  			{
  				if (reference.Check(versionDict, out versionFound))
  				{
  					//MessageService.ShowError(Plugin.Name + " conflicts with " + reference.ToString() + " and has been disabled.");
  					DisablePlugin(plugin, versionDict, pluginInfoDict);
  					goto checkDependencies; // after removing one Plugin, others could break
  				}
  			}
  			foreach (PluginReference reference in plugin.Manifest.Dependencies)
  			{
  				if (!reference.Check(versionDict, out versionFound))
  				{
  					if (versionFound != null)
  					{
  						//MessageService.ShowError(Plugin.Name + " has not been loaded because it requires " + reference.ToString() + ", but version " + versionFound.ToString() + " is installed.");
  					}
  					else
  					{
  						//MessageService.ShowError(Plugin.Name + " has not been loaded because it requires " + reference.ToString() + ".");
  					}
  					DisablePlugin(plugin, versionDict, pluginInfoDict);
  					goto checkDependencies; // after removing one Plugin, others could break
  				}
  			}
  		}
  	}

  	#endregion

    #region Private Methods

    // used by Load(): disables a Plugin and removes it from the dictionaries.
    private void DisablePlugin(PluginInfo Plugin, Dictionary<string, Version> versionDict, Dictionary<string, PluginInfo> pluginInfoDict)
    {
      Plugin.Enabled = false;
      //Plugin.Action = PluginAction.DependencyError;
      foreach (string name in Plugin.Manifest.Identities.Keys)
      {
        versionDict.Remove(name);
        pluginInfoDict.Remove(name);
      }
    }

    private PluginTreeNode CreatePath(PluginTreeNode localRoot, string path)
    {
      if (path == null || path.Length == 0)
      {
        return localRoot;
      }
      string[] splitPath = path.ToLower().Split('/');
      PluginTreeNode curPath = localRoot;
      int i = 0;
      while (i < splitPath.Length)
      {
        if (splitPath[i] != string.Empty)
        {
          if (!curPath.ChildNodes.ContainsKey(splitPath[i]))
          {
            curPath.ChildNodes[splitPath[i]] = new PluginTreeNode();
          }
          curPath = curPath.ChildNodes[splitPath[i]];
        }
        ++i;
      }

      return curPath;
    }

    private void AddExtensionPath(ExtensionPath path)
    {
      PluginTreeNode treePath = CreatePath(_rootNode, path.Name);
      foreach (NodeItem item in path.Items)
      {
        treePath.Items.Add(item);
      }
    }

    #endregion
  }
}
