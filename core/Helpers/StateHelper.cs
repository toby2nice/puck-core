﻿using Lucene.Net.Analysis;
using Newtonsoft.Json;
using puck.core.Abstract;
using puck.core.Base;
using puck.core.Constants;
using puck.core.Entities;
using puck.core.Models.EditorSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Lucene.Net.Analysis.Standard;
using puck.core.State;
using Lucene.Net.Analysis.Miscellaneous;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;

namespace puck.core.Helpers
{
    public static class StateHelper
    {
        public static I_Puck_Repository Repo{get{
                return PuckCache.PuckRepo;
        }}
        public static I_Task_Dispatcher tdispatcher{get{
                return PuckCache.PuckDispatcher;
        }}
        public static I_Content_Indexer indexer{get{
                return PuckCache.PuckIndexer;
        }}
        public static I_Api_Helper apiHelper { get { return PuckCache.ApiHelper; } }
        public static I_Log logger { get { return PuckCache.PuckLog; } }
        public static async Task SeedDb(IConfiguration config, IHostEnvironment env, IServiceProvider serviceProvider) {
            if (env.IsDevelopment())
            {
                using (var scope = serviceProvider.CreateScope())
                {
                    var userManager = (UserManager<PuckUser>)scope.ServiceProvider.GetService(typeof(UserManager<PuckUser>));
                    var roleManager = (RoleManager<PuckRole>)scope.ServiceProvider.GetService(typeof(RoleManager<PuckRole>));
                    var repo = scope.ServiceProvider.GetService<I_Puck_Repository>();
                    if (repo.GetPuckUser().Count() == 0)
                    {
                        var roles = new List<string> {PuckRoles.Cache,PuckRoles.Create,PuckRoles.Delete,PuckRoles.Domain,PuckRoles.Edit,PuckRoles.Localisation
                        ,PuckRoles.Move,PuckRoles.Notify,PuckRoles.Publish,PuckRoles.Puck,PuckRoles.Revert,PuckRoles.Settings,PuckRoles.Sort,PuckRoles.Tasks
                        ,PuckRoles.Unpublish,PuckRoles.Users,PuckRoles.Republish,PuckRoles.Copy,PuckRoles.ChangeType,PuckRoles.TimedPublish,PuckRoles.Audit};

                        foreach (var roleName in roles)
                        {
                            if (!await roleManager.RoleExistsAsync(roleName))
                            {
                                var role = new PuckRole();
                                role.Name = roleName;
                                await roleManager.CreateAsync(role);
                            }
                        }
                        var adminEmail = config.GetValue<string>("InitialUserEmail");
                        var adminPassword = config.GetValue<string>("InitialUserPassword");
                        if (!string.IsNullOrEmpty(adminEmail))
                        {
                            var admin = await userManager.FindByEmailAsync(adminEmail);
                            if (admin == null)
                            {
                                admin = new PuckUser { Email = adminEmail, UserName = adminEmail };
                                var result = await userManager.CreateAsync(admin, adminPassword);

                            }
                            //userManager.AddPassword(admin.Id, adminPassword);
                            foreach (var roleName in roles)
                            {
                                if (!await userManager.IsInRoleAsync(admin, roleName))
                                    await userManager.AddToRoleAsync(admin, roleName);
                            }
                        }


                    }
                

                }
            }
            

        }
        public static void UpdateCrops(bool addInstruction=false) {
            var settingsType = typeof(PuckImageEditorSettings);
            var modelType = typeof(BaseModel);
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetService<I_Puck_Repository>();
                string key = string.Concat(settingsType.FullName, ":", modelType.Name, ":");
                var meta = repo.GetPuckMeta().Where(x => x.Name == DBNames.EditorSettings && x.Key.Equals(key)).FirstOrDefault();
                if (meta != null)
                {
                    var data = JsonConvert.DeserializeObject(meta.Value, settingsType) as PuckImageEditorSettings;
                    if (data != null)
                    {
                        PuckCache.CropSizes = new Dictionary<string, Models.CropInfo>();
                        foreach (var crop in data.Crops ?? new List<Models.CropInfo>())
                        {
                            if (!string.IsNullOrEmpty(crop.Alias))
                                PuckCache.CropSizes[crop.Alias] = crop;
                        }
                    }
                }
                if (addInstruction)
                {
                    var instruction = new PuckInstruction();
                    instruction.InstructionKey = InstructionKeys.UpdateCrops;
                    instruction.Count = 1;
                    instruction.ServerName = ApiHelper.ServerName();
                    repo.AddPuckInstruction(instruction);
                    repo.SaveChanges();
                }
            }
        }

        public static void SetGeneratedMappings()
        {
            //var repo = Repo;
            var dictionary = new Dictionary<string, Type>();
            //var gmods = repo.GetGeneratedModel().ToList();
            //foreach (var mod in gmods)
            //{
            //    try
            //    {
            //        if (string.IsNullOrEmpty(mod.IFullPath) || string.IsNullOrEmpty(mod.CFullPath))
            //            continue;
            //        var idll = Assembly.LoadFrom(HttpContext.Current.Server.MapPath(mod.IFullPath));
            //        var cdll = Assembly.LoadFrom(HttpContext.Current.Server.MapPath(mod.CFullPath));

            //        AppDomain.CurrentDomain.Load(idll.GetName());
            //        AppDomain.CurrentDomain.Load(cdll.GetName());

            //        dictionary.Add(idll.GetTypes().First().AssemblyQualifiedName, cdll.GetTypes().First());
            //    }
            //    catch (Exception ex)
            //    {
            //        logger.Log(ex);
            //    }
            //}
            PuckCache.IGeneratedToModel = dictionary;
        }
        public static void SetFirstRequestUrl() {
            if (PuckCache.FirstRequestUrl==null)
                PuckCache.FirstRequestUrl = HttpContext.Current.Request.GetUri();
        }
        public static void UpdateTaskMappings(bool addInstruction=false)
        {
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetService<I_Puck_Repository>();
                var apiHelper = scope.ServiceProvider.GetService<I_Api_Helper>();
                var tasks = apiHelper.Tasks();
                tasks.AddRange(apiHelper.SystemTasks());
                //tasks = tasks.Where(x => tdispatcher.CanRun(x)).ToList();
                tasks.ForEach(x => x.TaskEnd += tdispatcher.HandleTaskEnd);
                tdispatcher.Tasks = tasks;
                if (addInstruction)
                {
                    var instruction = new PuckInstruction();
                    instruction.InstructionKey = InstructionKeys.UpdateTaskMappings;
                    instruction.Count = 1;
                    instruction.ServerName = ApiHelper.ServerName();
                    repo.AddPuckInstruction(instruction);
                    repo.SaveChanges();
                }
            }
        }
        //update class hierarchies/typechains which may have changed since last run
        public static void UpdateTypeChains()
        {
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetService<I_Puck_Repository>();
                var excluded = new List<Type> { typeof(puck.core.Entities.PuckRevision) };
                var currentTypes = ApiHelper.FindDerivedClasses(typeof(puck.core.Base.BaseModel), excluded: excluded, inclusive: false);
                var meta = repo.GetPuckMeta().Where(x => x.Name == DBNames.TypeChain).ToList();
                var typesToUpdate = new List<Type>();
                foreach (var item in meta)
                {
                    //check saved type is in currentTypes
                    var type = currentTypes.FirstOrDefault(x => x.Name.Equals(item.Key));
                    if (type != null)
                    {
                        var typeChain = ApiHelper.TypeChain(type);
                        var dbTypeChain = item.Value;
                        //check that typechain is the same
                        //if not, add to types to update
                        if (!typeChain.Equals(dbTypeChain))
                        {
                            typesToUpdate.Add(type);
                        }
                    }
                }
                var toIndex = new List<BaseModel>();
                foreach (var type in typesToUpdate)
                {
                    //get revisions whose typechains have changed
                    var revisions = repo.GetPuckRevision().Where(x => x.Type.Equals(type.Name));
                    foreach (var revision in revisions)
                    {
                        //update typechain in revision and in model which may need to be published
                        revision.TypeChain = ApiHelper.TypeChain(type);
                        var model = ApiHelper.RevisionToBaseModel(revision);
                        model.TypeChain = ApiHelper.TypeChain(type);
                        revision.Value = JsonConvert.SerializeObject(model);
                        if (model.Published && revision.Current)
                            toIndex.Add(model);
                    }
                    repo.SaveChanges();
                }
                //publish content with updated typechains
                indexer.Index(toIndex);
                //delete typechains from previous bootstrap
                meta.ForEach(x => repo.DeleteMeta(x));
                repo.SaveChanges();
                //save typechains from current bootstrap
                currentTypes.ToList().ForEach(x =>
                {
                    var newMeta = new PuckMeta
                    {
                        Name = DBNames.TypeChain,
                        Key = x.Name,
                        Value = ApiHelper.TypeChain(x)
                    };
                    repo.AddMeta(newMeta);
                });
                repo.SaveChanges();
            }
        }
        public static void UpdateRedirectMappings(bool addInstruction=false)
        {
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetService<I_Puck_Repository>();
                var meta301 = repo.GetPuckRedirect().Where(x => x.Type == "301").ToList();
                var meta302 = repo.GetPuckRedirect().Where(x => x.Type == "302").ToList();
                var map301 = new Dictionary<string, string>();
                meta301.ForEach(x =>
                {
                //map301.Add(x.Key.ToLower(), x.Value.ToLower());
                map301[x.From.ToLower()] = x.To.ToLower();
                });
                var map302 = new Dictionary<string, string>();
                meta302.ForEach(x =>
                {
                //map302.Add(x.Key.ToLower(), x.Value.ToLower());
                map302[x.From.ToLower()] = x.To.ToLower();
                });
                PuckCache.Redirect301 = map301;
                PuckCache.Redirect302 = map302;
                if (addInstruction)
                {
                    var instruction = new PuckInstruction();
                    instruction.InstructionKey = InstructionKeys.UpdateRedirects;
                    instruction.Count = 1;
                    instruction.ServerName = ApiHelper.ServerName();
                    repo.AddPuckInstruction(instruction);
                    repo.SaveChanges();
                }
            }
        }
        public static void UpdateCacheMappings(bool addInstruction=false)
        {
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetService<I_Puck_Repository>();
                var metaTypeCache = repo.GetPuckMeta().Where(x => x.Name == DBNames.CachePolicy).ToList();
                var metaCacheExclude = repo.GetPuckMeta().Where(x => x.Name == DBNames.CacheExclude).ToList();

                var mapTypeCache = new Dictionary<string, int>();
                metaTypeCache.ForEach(x =>
                {
                    int cacheMinutes;
                    if (int.TryParse(x.Value, out cacheMinutes))
                    {
                    //mapTypeCache.Add(x.Key, cacheMinutes);
                    mapTypeCache[x.Key] = cacheMinutes;
                    }
                });

                var mapCacheExclude = new HashSet<string>();
                metaCacheExclude.Where(x => x.Value.ToLower() == bool.TrueString.ToLower()).ToList().ForEach(x =>
                {
                    if (!mapCacheExclude.Contains(x.Key.ToLower()))
                        mapCacheExclude.Add(x.Key.ToLower());
                });
                PuckCache.TypeOutputCache = mapTypeCache;
                PuckCache.OutputCacheExclusion = mapCacheExclude;

                if (addInstruction)
                {
                    var instruction = new PuckInstruction();
                    instruction.InstructionKey = InstructionKeys.UpdateCacheMappings;
                    instruction.Count = 1;
                    instruction.ServerName = ApiHelper.ServerName();
                    repo.AddPuckInstruction(instruction);
                    repo.SaveChanges();
                }
            }
        }
        public static void UpdateDomainMappings(bool addInstruction=false)
        {
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetService<I_Puck_Repository>();
                var meta = repo.GetPuckMeta().Where(x => x.Name == DBNames.DomainMapping).ToList();
                var map = new Dictionary<string, string>();
                meta.ForEach(x =>
                {
                    //map.Add(x.Value.ToLower(), x.Key.ToLower());
                    map[x.Value.ToLower()] = x.Key.ToLower();
                });
                PuckCache.DomainRoots = map;
                if (addInstruction)
                {
                    var instruction = new PuckInstruction();
                    instruction.InstructionKey = InstructionKeys.UpdateDomainMappings;
                    instruction.Count = 1;
                    instruction.ServerName = ApiHelper.ServerName();
                    repo.AddPuckInstruction(instruction);
                    repo.SaveChanges();
                }
            }
        }
        public static void UpdatePathLocaleMappings(bool addInstruction=false)
        {
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetService<I_Puck_Repository>();
                var meta = repo.GetPuckMeta().Where(x => x.Name == DBNames.PathToLocale).OrderByDescending(x => x.Key.Length).ToList();
                var map = new Dictionary<string, string>();
                meta.ForEach(x =>
                {
                //map.Add(x.Key.ToLower(), x.Value.ToLower());
                map[x.Key.ToLower()] = x.Value.ToLower();
                });
                PuckCache.PathToLocale = map;

                if (addInstruction)
                {
                    var instruction = new PuckInstruction();
                    instruction.InstructionKey = InstructionKeys.UpdatePathLocales;
                    instruction.Count = 1;
                    instruction.ServerName = ApiHelper.ServerName();
                    repo.AddPuckInstruction(instruction);
                    repo.SaveChanges();
                }
            }
        }
        public static void UpdateAQNMappings()
        {
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var apiHelper = scope.ServiceProvider.GetService<I_Api_Helper>();
                foreach (var t in apiHelper.AllModels(true))
                {
                    if (PuckCache.ModelNameToAQN.ContainsKey(t.Name))
                        throw new Exception($"there is more than one ViewModel with the name:{t.Name}. ViewModel names must be unique!");
                    PuckCache.ModelNameToAQN[t.Name] = t.AssemblyQualifiedName;
                }
            }
        }
        public static void UpdateAnalyzerMappings()
        {
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var apiHelper = scope.ServiceProvider.GetService<I_Api_Helper>();
                var panalyzers = new List<Analyzer>();
                var analyzerForModel = new Dictionary<Type, Analyzer>();
                foreach (var t in apiHelper.AllModels(true))
                {
                    var instance = ApiHelper.CreateInstance(t);
                    try
                    {
                        ObjectDumper.SetPropertyValues(instance);
                    }
                    catch (Exception ex)
                    {
                        PuckCache.PuckLog.Log(ex);
                    };

                    var dmp = ObjectDumper.Write(instance, int.MaxValue);
                    var analyzers = new Dictionary<string, Analyzer>();
                    PuckCache.TypeFields[t.AssemblyQualifiedName] = new Dictionary<string, string>();
                    foreach (var p in dmp)
                    {
                        if (!PuckCache.TypeFields[t.AssemblyQualifiedName].ContainsKey(p.Key))
                            PuckCache.TypeFields[t.AssemblyQualifiedName].Add(p.Key, p.Type.AssemblyQualifiedName);
                        if (p.Analyzer == null)
                            continue;
                        if (!panalyzers.Any(x => x.GetType() == p.Analyzer.GetType()))
                        {
                            panalyzers.Add(p.Analyzer);
                        }
                        analyzers.Add(p.Key, panalyzers.Where(x => x.GetType() == p.Analyzer.GetType()).FirstOrDefault());
                    }
                    var pfAnalyzer = new PerFieldAnalyzerWrapper(new StandardAnalyzer(Lucene.Net.Util.LuceneVersion.LUCENE_48), analyzers);
                    analyzerForModel.Add(t, pfAnalyzer);
                }
                PuckCache.Analyzers = panalyzers;
                PuckCache.AnalyzerForModel = analyzerForModel;
            }
        }
        public static void UpdateDefaultLanguage()
        {
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetService<I_Puck_Repository>();
                var meta = repo.GetPuckMeta().Where(x => x.Name == DBNames.Settings && x.Key == DBKeys.DefaultLanguage).FirstOrDefault();
                if (meta != null && !string.IsNullOrEmpty(meta.Value))
                    PuckCache.SystemVariant = meta.Value;
            }
        }
        public static void SetModelDerivedMappings() {
            using (var scope = PuckCache.ServiceProvider.CreateScope())
            {
                var apiHelper = scope.ServiceProvider.GetService<I_Api_Helper>();
                PuckCache.ModelDerivedModels = new Dictionary<string, List<Type>>();
                var modelTypes = apiHelper.Models();
                foreach (var modelType in modelTypes)
                {
                    //var derivedClasses = ApiHelper.TypeChainType(modelType);
                    var derivedClasses = ApiHelper.FindDerivedClasses(modelType,inclusive:true).ToList();
                    PuckCache.ModelDerivedModels[modelType.Name] = derivedClasses;
                }
            }
        }

    }
}
