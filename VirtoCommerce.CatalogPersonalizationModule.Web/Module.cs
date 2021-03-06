﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Practices.Unity;
using VirtoCommerce.CatalogModule.Data.Search;
using VirtoCommerce.CatalogPersonalizationModule.Core.Services;
using VirtoCommerce.CatalogPersonalizationModule.Data.Model;
using VirtoCommerce.CatalogPersonalizationModule.Data.Repositories;
using VirtoCommerce.CatalogPersonalizationModule.Data.Search;
using VirtoCommerce.CatalogPersonalizationModule.Data.Search.Indexing;
using VirtoCommerce.CatalogPersonalizationModule.Data.Services;
using VirtoCommerce.CatalogPersonalizationModule.Web.ExportImport;
using VirtoCommerce.Domain.Search;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.Platform.Data.Infrastructure.Interceptors;
using VirtoCommerce.Platform.Data.Repositories;

namespace VirtoCommerce.CatalogPersonalizationModule.Web
{
    public class Module : ModuleBase, ISupportExportImportModule
    {
        private const string _connectionStringName = "VirtoCommerce";
        private readonly IUnityContainer _container;

        public Module(IUnityContainer container)
        {
            _container = container;
        }

        #region IModule Members

        public override void SetupDatabase()
        {
            using (var context = new PersonalizationRepositoryImpl(_connectionStringName, _container.Resolve<AuditableInterceptor>()))
            {
                var initializer = new SetupDatabaseInitializer<PersonalizationRepositoryImpl, Data.Migrations.Configuration>();
                initializer.InitializeDatabase(context);
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            _container.RegisterType<IPersonalizationRepository>(new InjectionFactory(c => new PersonalizationRepositoryImpl(_connectionStringName, new EntityPrimaryKeyGeneratorInterceptor(), _container.Resolve<AuditableInterceptor>(),
                new ChangeLogInterceptor(_container.Resolve<Func<IPlatformRepository>>(), ChangeLogPolicy.Cumulative, new[] { typeof(TaggedItemEntity).Name }))));
            _container.RegisterType<ITaggedItemService, PersonalizationService>();
            _container.RegisterType<ITaggedItemSearchService, PersonalizationService>();

            //_container.RegisterType<ProductSearchUserGroupsRequestBuilder>();
            _container.RegisterType<ISearchRequestBuilder, ProductSearchUserGroupsRequestBuilder>(nameof(ProductSearchRequestBuilder));
        }

        public override void PostInitialize()
        {
            base.PostInitialize();

            #region Search

            // Add tagged items document source to the category or product indexing configuration
            var documentIndexingConfigurations = _container.Resolve<IndexDocumentConfiguration[]>();
            if (documentIndexingConfigurations != null)
            {
                //Category indexing
                var taggedItemCategoryDocumentSource = new IndexDocumentSource
                {
                    ChangesProvider = _container.Resolve<TaggedItemIndexChangesProvider>(),
                    DocumentBuilder = _container.Resolve<TaggedItemCategoryDocumentBuilder>(),
                };
                foreach (var configuration in documentIndexingConfigurations.Where(c => c.DocumentType == KnownDocumentTypes.Category))
                {
                    if (configuration.RelatedSources == null)
                    {
                        configuration.RelatedSources = new List<IndexDocumentSource>();
                    }

                    configuration.RelatedSources.Add(taggedItemCategoryDocumentSource);
                }

                //Product indexing
                var taggedItemProductDocumentSource = new IndexDocumentSource
                {
                    ChangesProvider = _container.Resolve<TaggedItemIndexChangesProvider>(),
                    DocumentBuilder = _container.Resolve<TaggedItemProductDocumentBuilder>(),
                };
                foreach (var configuration in documentIndexingConfigurations.Where(c => c.DocumentType == KnownDocumentTypes.Product))
                {
                    if (configuration.RelatedSources == null)
                    {
                        configuration.RelatedSources = new List<IndexDocumentSource>();
                    }

                    configuration.RelatedSources.Add(taggedItemProductDocumentSource);
                }
            }

            #endregion
        }

        #endregion

        #region ISupportExportImportModule Members

        public void DoExport(System.IO.Stream outStream, PlatformExportManifest manifest, Action<ExportImportProgressInfo> progressCallback)
        {
            var exportJob = _container.Resolve<PersonalizationExportImport>();
            exportJob.DoExport(outStream, progressCallback);
        }

        public void DoImport(System.IO.Stream inputStream, PlatformExportManifest manifest, Action<ExportImportProgressInfo> progressCallback)
        {
            var exportJob = _container.Resolve<PersonalizationExportImport>();
            exportJob.DoImport(inputStream, progressCallback);
        }

        public string ExportDescription
        {
            get
            {
                var settingManager = _container.Resolve<ISettingsManager>();
                return settingManager.GetValue("Personalization.ExportImport.Description", string.Empty);
            }
        }

        #endregion
    }
}
