﻿using CouchDB.Driver.DTOs;
using CouchDB.Driver.Extensions;
using CouchDB.Driver.Helpers;
using CouchDB.Driver.Types;
using Flurl.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CouchDB.Driver
{
    public class CouchDatabase<TSource> where TSource : CouchEntity
    {
        private readonly QueryProvider _queryProvider;
        private readonly FlurlClient _flurlClient;
        private readonly CouchSettings _settings;
        private readonly string _connectionString;
        public string Database { get; }

        internal CouchDatabase(FlurlClient flurlClient, CouchSettings settings, string connectionString, string db)
        {
            _flurlClient = flurlClient ?? throw new ArgumentNullException(nameof(flurlClient));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Database = db ?? throw new ArgumentNullException(nameof(db));
            _queryProvider = new CouchQueryProvider(flurlClient, _settings, connectionString, Database);
        }

        public IQueryable<TSource> AsQueryable()
        {
            return new CouchQuery<TSource>(_queryProvider);
        }
        
        #region Query

        public List<TSource> ToList()
        {
            return AsQueryable().ToList();
        }
        public Task<List<TSource>> ToListAsync()
        {
            return AsQueryable().ToListAsync();
        }
        public ICouchList<TSource> ToCouchList()
        {
            return AsQueryable().ToCouchList();
        }
        public Task<ICouchList<TSource>> ToCouchListAsync()
        {
            return AsQueryable().ToCouchListAsync();
        }
        public IQueryable<TSource> Where(Expression<Func<TSource, bool>> predicate)
        {
            return AsQueryable().Where(predicate);
        }
        public IOrderedQueryable<TSource> OrderBy<TKey>(Expression<Func<TSource, TKey>> keySelector)
        {
            return AsQueryable().OrderBy(keySelector);
        }
        public IOrderedQueryable<TSource> OrderByDescending<TKey>(Expression<Func<TSource, TKey>> keySelector)
        {
            return AsQueryable().OrderByDescending(keySelector);
        }
        public IQueryable<TResult> Select<TResult>(Expression<Func<TSource, TResult>> selector)
        {
            return AsQueryable().Select(selector);
        }
        public IQueryable<TSource> Skip(int count)
        {
            return AsQueryable().Skip(count);
        }
        public IQueryable<TSource> Take(int count)
        {
            return AsQueryable().Take(count);
        }
        public IQueryable<TSource> UseBookmark(string bookmark)
        {
            return AsQueryable().UseBookmark(bookmark);
        }
        public IQueryable<TSource> WithReadQuorum(int quorum)
        {
            return AsQueryable().WithReadQuorum(quorum);
        }
        public IQueryable<TSource> WithoutIndexUpdate()
        {
            return AsQueryable().WithoutIndexUpdate();
        }
        public IQueryable<TSource> FromStable()
        {
            return AsQueryable().FromStable();
        }
        public IQueryable<TSource> UseIndex(params string[] indexes)
        {
            return AsQueryable().UseIndex(indexes);
        }
        public IQueryable<TSource> IncludeExecutionStats()
        {
            return AsQueryable().IncludeExecutionStats();
        }

        #endregion

        #region Find

        public async Task<TSource> FindAsync(string docId)
        {
            return await NewRequest()
                .AppendPathSegment("doc")
                .AppendPathSegment(docId)
                .GetJsonAsync<TSource>()
                .SendRequestAsync();
        }

        #endregion

        #region Writing

        public async Task<TSource> CreateAsync(TSource item)
        {
            var response = await NewRequest()
                .PostJsonAsync(item)
                .ReceiveJson<DocumentSaveResponse>()
                .SendRequestAsync();
            return (TSource)item.ProcessSaveResponse(response);
        }
        public async Task<TSource> CreateOrUpdateAsync(TSource item)
        {
            if (string.IsNullOrEmpty(item.Id))
                throw new InvalidOperationException("Cannot add or update an entity without an ID.");

            var response = await NewRequest()
                .AppendPathSegment("doc")
                .AppendPathSegment(item.Id)
                .PutJsonAsync(item)
                .ReceiveJson<DocumentSaveResponse>()
                .SendRequestAsync();

            return (TSource)item.ProcessSaveResponse(response);
        }
        public async Task DeleteAsync(TSource document)
        {
            await NewRequest()
                .AppendPathSegment("doc")
                .AppendPathSegment(document.Id)
                .SetQueryParam("rev", document.Rev)
                .DeleteAsync()
                .SendRequestAsync();
        }
        public async Task<IEnumerable<TSource>> CreateOrUpdateRangeAsync(IEnumerable<TSource> documents)
        {
            var response = await NewRequest()
                .AppendPathSegment("_bulk_docs")
                .PostJsonAsync(new { Docs = documents })
                .ReceiveJson<DocumentSaveResponse[]>()
                .SendRequestAsync();

            var zipped = documents.Zip(response, (doc, saveResponse) => (Document: doc, SaveResponse: saveResponse));
            foreach (var (document, saveResponse) in zipped)
                document.ProcessSaveResponse(saveResponse);
            return documents;
        }

        #endregion

        #region Utils

        public async Task CompactAsync()
        {
            await NewRequest()
                .AppendPathSegment("_compact")
                .PostJsonAsync(null)
                .SendRequestAsync();
        }
        public async Task<CouchDatabaseInfo> GetInfoAsync()
        {
            return await NewRequest()
                .GetJsonAsync<CouchDatabaseInfo>()
                .SendRequestAsync();
        }

        #endregion

        #region Override

        public override string ToString()
        {
            return AsQueryable().ToString();
        }

        #endregion

        #region Helper

        private IFlurlRequest NewRequest()
        {
            return _flurlClient.Request(_connectionString).AppendPathSegment(Database);
        }

        #endregion
    }
}