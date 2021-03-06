﻿using Ccf.Ck.Utilities.MemoryCache;
using Ccf.Ck.Models.Settings.Modules;
using Ccf.Ck.Utilities.DependencyContainer;
using Ccf.Ck.Utilities.Generic.Topologies;
using Ccf.Ck.Utilities.Web.BundleTransformations.Primitives;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.IO;
using static Ccf.Ck.Utilities.Web.BundleTransformations.Primitives.TemplateKraftBundle;
using Ccf.Ck.Models.Settings;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace Ccf.Ck.Models.KraftModule
{
    public class KraftModule : IDependable<KraftModule>
    {
        //some consts, that should be extracted further
        const string CSS_FOLDER_NAME = "Css";
        const string JS_FOLDER_NAME = "Scripts";
        const string TEMPLATES_FOLDER_NAME = "Templates";
        const string DEPENDENCY_FILE_NAME = "Dependency.json";
        const string CONFIGURATION_FILE_NAME = "Configuration.json";
        const string RESOURCEDEPENDENCY_FILE_NAME = "Module.dep";

        private readonly DependencyInjectionContainer _DependencyInjectionContainer;
        private readonly KraftModuleCollection _ModuleCollection;
        private readonly ILogger _Logger;
        private readonly string _ModulePath;

        public ScriptKraftBundle ScriptKraftBundle { get; private set; }
        public TemplateKraftBundle TemplateKraftBundle { get; private set; }
        public StyleKraftBundle StyleKraftBundle { get; private set; }
        public KraftModuleConfigurationSettings ModuleSettings { get; private set; }
        public KraftModuleRootConf KraftModuleRootConf { get; private set; }
        public string Key
        {
            get
            {
                return _ModuleCollection?.ConstructValidKey(KraftModuleRootConf?.Name);
            }
        }
        public bool IsInitialized { get; private set; }
        public string DirectoryName { get; private set; }
        public int DependencyOrderIndex { get; internal set; }
        public IDictionary<string, IDependable<KraftModule>> Dependencies { get; internal set; }

        internal KraftModule(string directoryName, string moduleName, DependencyInjectionContainer dependencyInjectionContainer, KraftModuleCollection moduleCollection, ICachingService cachingService, KraftGlobalConfigurationSettings kraftGlobalConfigurationSettings, ILogger logger)
        {
            _DependencyInjectionContainer = dependencyInjectionContainer;
            _ModuleCollection = moduleCollection;
            _Logger = logger;
            DirectoryName = directoryName;

            //dependencies
            Dependencies = new Dictionary<string, IDependable<KraftModule>>();

            //Bundles initialize
            StyleKraftBundle = null;
            ScriptKraftBundle = null;
            TemplateKraftBundle = null;

            _ModulePath = Path.Combine(DirectoryName, moduleName);
            if (!Directory.Exists(_ModulePath))
            {
                throw new DirectoryNotFoundException($"The {_ModulePath} path was not found!");
            }

            //read configs and dependencies
            IsInitialized = ReadModuleMetaConfiguration(Path.Combine(_ModulePath, DEPENDENCY_FILE_NAME), _ModulePath);
            if (IsInitialized)
            {
                InitConfiguredPlugins(Key, Path.Combine(_ModulePath, CONFIGURATION_FILE_NAME), cachingService);
            }
        }

        public void ConstructResources(ICachingService cachingService, KraftGlobalConfigurationSettings kraftGlobalConfigurationSettings, string startDepFile, bool isScript)
        {
            if (IsInitialized)//This is not a module with proper configuration files
            {
                if (isScript)
                {
                    //process Scripts folder
                    ScriptKraftBundle = ConstructScriptsBundle(kraftGlobalConfigurationSettings, Path.Combine(_ModulePath, JS_FOLDER_NAME), startDepFile);

                    //process Template folder               
                    TemplateKraftBundle = ConstructTmplResBundle(kraftGlobalConfigurationSettings, Path.Combine(_ModulePath, TEMPLATES_FOLDER_NAME));
                }
                else
                {
                    //process CSS folder
                    StyleKraftBundle = ConstructStyleBundle(kraftGlobalConfigurationSettings, Path.Combine(_ModulePath, CSS_FOLDER_NAME), startDepFile);
                }
            }
        }

        private bool ReadModuleMetaConfiguration(string packageJson, string modulePath)
        {
            try
            {
                if (File.Exists(packageJson))
                {
                    using (StreamReader r = new StreamReader(packageJson))
                    {
                        Dictionary<string, string> depVersion = new Dictionary<string, string>();
                        KraftModuleRootConf = JsonConvert.DeserializeObject<KraftModuleRootConf>(r.ReadToEnd());
                        foreach (KeyValuePair<string, string> item in KraftModuleRootConf.Dependencies)
                        {
                            depVersion.Add(item.Key.ToLower(), item.Value);
                        }
                        KraftModuleRootConf.Dependencies = depVersion;
                        return true;
                    }
                }
                else
                {
                    //Obviously this folder doesn't contain configuration file, so we are not handling it anymore
                    return false;
                }
            }
            catch (Exception boom)
            {
                throw new Exception($"Reading module's meta configuration file failed for module \"{modulePath}\". {boom.Message}");
            }
        }

        private void InitConfiguredPlugins(string moduleKey, string configFile, ICachingService cachingService)
        {
            if (!File.Exists(configFile))
            {
                throw new FileNotFoundException($"The {configFile} file was not found!");
            }

            //Init the kraft module configurations model
            ModuleSettings = new KraftModuleConfigurationSettings(_DependencyInjectionContainer, cachingService);

            //read the module configuration
            IConfigurationBuilder configbuilder = new ConfigurationBuilder();
            configbuilder.SetBasePath(Path.GetDirectoryName(configFile)).AddJsonFile(configFile);
            IConfigurationRoot configurationRoot = configbuilder.Build();
            configurationRoot.Bind("KraftModuleConfigurationSettings", ModuleSettings);

            ModuleSettings.LoadDefinedObjects(moduleKey, configFile);
        }

        internal void ConstructDependencies()
        {
            Dependencies = new Dictionary<string, IDependable<KraftModule>>();

            foreach (KeyValuePair<string, string> dependency in KraftModuleRootConf.Dependencies)
            {
                IDependable<KraftModule> depModule = _ModuleCollection.GetModuleAsDependable(dependency.Key);
                if (depModule == null)
                {
                    throw new Exception($"No module with a key \"{dependency.Key}\" is loaded!");
                }
                Dependencies.Add(depModule.Key, depModule);
            }
        }

        private StyleKraftBundle ConstructStyleBundle(KraftGlobalConfigurationSettings kraftGlobalConfigurationSettings, string resFolderPath, string resProfileFileName)
        {
            string resProfilePhysFileName = Path.Combine(resFolderPath, resProfileFileName);
            if (Directory.Exists(resFolderPath) && File.Exists(resProfilePhysFileName))
            {
                StyleKraftBundle resBundle = new StyleKraftBundle();
                resBundle.ContentRootPath = kraftGlobalConfigurationSettings.EnvironmentSettings.ContentRootPath;
                //KraftBundle resBundle = new StyleKraftBundle() { ContentRootPath = _KraftEnvironmentSettings.ContentRootPath };
                KraftBundleProfiles resBundleProfile = resBundle as KraftBundleProfiles;
                resBundleProfile.ContentRootPath = kraftGlobalConfigurationSettings.EnvironmentSettings.ContentRootPath;
                if (resBundleProfile != null)
                {
                    resBundleProfile.StartDirPath = resFolderPath;
                    resBundleProfile.ProfileFiles = new List<string> { RESOURCEDEPENDENCY_FILE_NAME, resProfileFileName };
                    return resBundle;
                }
            }
            return null;
        }

        private ScriptKraftBundle ConstructScriptsBundle(KraftGlobalConfigurationSettings kraftGlobalConfigurationSettings, string resFolderPath, string resProfileFileName)
        {
            string resProfilePhysFileName = Path.Combine(resFolderPath, resProfileFileName);
            if (Directory.Exists(resFolderPath) && File.Exists(resProfilePhysFileName))
            {
                ScriptKraftBundle resBundle = new ScriptKraftBundle();
                resBundle.ContentRootPath = kraftGlobalConfigurationSettings.EnvironmentSettings.ContentRootPath;
                //KraftBundle resBundle = new StyleKraftBundle() { ContentRootPath = _KraftEnvironmentSettings.ContentRootPath };
                KraftBundleProfiles resBundleProfile = resBundle as KraftBundleProfiles;
                resBundleProfile.ContentRootPath = kraftGlobalConfigurationSettings.EnvironmentSettings.ContentRootPath;
                if (resBundleProfile != null)
                {
                    resBundleProfile.StartDirPath = resFolderPath;
                    resBundleProfile.ProfileFiles = new List<string> { RESOURCEDEPENDENCY_FILE_NAME, resProfileFileName };
                    return resBundle;
                }
            }
            return null;
        }

        private TemplateKraftBundle ConstructTmplResBundle(KraftGlobalConfigurationSettings kraftGlobalConfigurationSettings, string tmplFolderPath)
        {
            if (Directory.Exists(tmplFolderPath))
            {
                TemplateKraftBundle resBundle = new TemplateKraftBundle { ContentRootPath = kraftGlobalConfigurationSettings.EnvironmentSettings.ContentRootPath };
                IFileProvider fileProv = new PhysicalFileProvider(tmplFolderPath);
                IDirectoryContents dirContents = fileProv.GetDirectoryContents("");
                resBundle.StartDirPath = tmplFolderPath;
                resBundle.ModuleName = Key;
                foreach (IFileInfo file in dirContents)
                {
                    string fileExtension = Path.GetExtension(file.PhysicalPath);
                    TemplateFile templateFile = new TemplateFile { TemplateName = file.Name.ToLower().Replace(fileExtension, string.Empty), PhysicalPath = file.PhysicalPath };
                    resBundle.TemplateFiles.Add(templateFile);
                }
                return resBundle;
            }
            return null;
        }
    }
}
