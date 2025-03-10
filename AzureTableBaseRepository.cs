using Azure;
using Azure.Data.Tables;

namespace BrianCaos.Foundation
{
  public abstract class AzureTableBaseRepository<T> where T : class, ITableEntity
  {
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableClient _tableClient;

    public AzureTableBaseRepository(string tableName, TableServiceClient tableServiceClient)
    {
      _tableServiceClient = tableServiceClient;
      _tableClient = _tableServiceClient.GetTableClient(tableName);
      _tableClient.CreateIfNotExists();
    } 

    /// <summary>
    /// Adds a Table Entity of type <see cref="T"/> into the Table.
    /// </summary>
    /// <param name="entity">A custom model type that implements ITableEntity or an instance of TableEntity</param>
    /// <returns></returns>
    public async Task AddAsync(T entity) 
    {
      await _tableClient.AddEntityAsync(entity);
    }

    /// <summary>
    /// Adds a list of Table Entity of type <see cref="T"/> into the Table.
    /// </summary>
    /// <param name="entities">A list of custom model types that implements ITableEntity or an instance of TableEntity</param>
    /// <returns></returns>
    public async Task AddAsync(IEnumerable<T> entities) 
    {
      await SubmitTransactionAsync(entities, TableTransactionActionType.Add);
    }

    /// <summary>
    /// Unconditionally replaces the specified table entity of type <see cref="T"/>, if it exists. 
    /// </summary>
    /// <param name="entity">A custom model type that implements ITableEntity or an instance of TableEntity</param>
    /// <returns></returns>
    public async Task UpdateAsync(T entity) 
    {
      await _tableClient.UpdateEntityAsync(entity, Azure.ETag.All, TableUpdateMode.Replace);
    }

    /// <summary>
    /// Updates the specified table entity of type <see cref="T"/>, if it exists. 
    /// If the mode is Replace, the entity will be replaced. 
    /// If the mode is Merge, the property values present in the entity 
    /// will be merged with the existing entity.
    /// </summary>
    /// <param name="entity">A custom model type that implements ITableEntity or an instance of TableEntity</param>
    /// <param name="ifMatch">
    /// The If-Match value to be used for optimistic concurrency. 
    /// If All is specified, the operation will be executed unconditionally. 
    /// If the ETag value is specified, the operation will fail with a status 
    /// of 412 (Precondition Failed) if the ETag value of the entity in the 
    /// table does not match.
    /// </param>
    /// <param name="tableUpdateMode">Determines the behavior of the Update operation.</param>
    /// <returns></returns>
    public async Task UpdateAsync(T entity, ETag ifMatch, TableUpdateMode tableUpdateMode) 
    {
      await _tableClient.UpdateEntityAsync(entity, ifMatch, tableUpdateMode);
    }

    /// <summary>
    /// Unconditionally replaces the specified list table entities of type <see cref="T"/>, if they exists. 
    /// </summary>
    /// <param name="entities">A list of custom model types that implements ITableEntity or an instance of TableEntity</param>
    /// <returns></returns>
    public async Task UpdateAsync(IEnumerable<T> entities) 
    {
      await SubmitTransactionAsync(entities, TableTransactionActionType.UpdateReplace);
    }

    /// <summary>
    /// Replaces the specified table entity of type <see cref="T"/>, if it exists. 
    /// Creates the entity if it does not exist.
    /// </summary>
    /// <param name="entity">A custom model type that implements ITableEntity or an instance of TableEntity</param>
    /// <returns></returns>
    public async Task UpsertAsync(T entity) 
    {
      await _tableClient.UpsertEntityAsync(entity);
    }

    /// <summary>
    /// Replaces the specified list of table entity of type <see cref="T"/>, if it exists. 
    /// Creates the entities if they does not exist.
    /// </summary>
    /// <param name="entities">A list of custom model types that implements ITableEntity or an instance of TableEntity</param>
    /// <returns></returns>
    public async Task UpsertAsync(IEnumerable<T> entities) 
    {
      await SubmitTransactionAsync(entities, TableTransactionActionType.UpsertReplace);
    }

    /// <summary>
    /// Deletes the specified table entity.
    /// </summary>
    /// <param name="partitionKey">The partitionKey that identifies the table entity/param>
    /// <param name="rowKey">The rowKey that identifies the table entity</param>
    /// <returns></returns>
    public async Task DeleteAsync(string partitionKey, string rowKey)
    {
      await _tableClient.DeleteEntityAsync(partitionKey, rowKey);
    }

    /// <summary>
    /// Deletes the list of specified table entities
    /// </summary>
    /// <param name="entities">A list of custom model types that implements ITableEntity or an instance of TableEntity</param>
    /// <returns></returns>
    public async Task DeleteAsync(IEnumerable<T> entities) 
    {
      await SubmitTransactionAsync(entities, TableTransactionActionType.Delete);
    }

    /// <summary>
    /// Gets the specified table entity of type <see cref="T"/>
    /// </summary>
    /// <param name="partitionKey">The partitionKey that identifies the table entity/param>
    /// <param name="rowKey">The rowKey that identifies the table entity</param>
    /// <returns></returns>
    public async Task<T> GetAsync(string partitionKey, string rowKey) 
    {
      return await _tableClient.GetEntityAsync<T>(partitionKey, rowKey);
    }

    /// <summary>
    /// Get a list of table entities of type <see cref="T"/>
    /// </summary>
    /// <param name="filter">Query that specifies which entities to return</param>
    /// <returns>A <see cref="IEnumerable"/> list of entities matcing the query specified</returns>
    public async Task<IEnumerable<T>> GetAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter) 
    {
      string? continuationToken = null;
      List<T> allMessages = new List<T>();
      do
      {
        var page = await GetAsync(filter, continuationToken);
        continuationToken = page.Item1;
        allMessages.AddRange(page.Item2);
      } while (continuationToken != null);

      return allMessages;
    }

    private async Task<Tuple<string, IEnumerable<T>>?> GetAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, string? continuationToken)
    {
      var messages = _tableClient.QueryAsync(filter, maxPerPage: 1000);

      await foreach (var page in messages.AsPages(continuationToken))
      {
        return Tuple.Create<string, IEnumerable<T>>(page.ContinuationToken, page.Values);
      }
      return null;
    }

    private async Task SubmitTransactionAsync(IEnumerable<T> entities, TableTransactionActionType tableTransactionActionType)
    {
      foreach (var batch in entities.Chunk(100))
      {
        List<TableTransactionAction> action = new List<TableTransactionAction>();
        foreach (var entity in batch)
        {
          action.Add(new TableTransactionAction(tableTransactionActionType, entity));
        }
        await _tableClient.SubmitTransactionAsync(action);
      }
    }

  }
}
