# AzureTableRepository

A simple Azure Table base class implementation, simplifying CRUD operations on Azure Table.

- Use strong types.
- Read an entire table without paging.
- No size limit when bulk insert, update, delete. 

# How to Setup the base class

## STEP 1: Implement a ITableEntity class

This class is the contents of your table. This is an example class:

```csharp
  public class MyEntity : ITableEntity
  {
    /// <summary>
    /// Mandatory field (partition id)
    /// </summary>
    public string? PartitionKey { get; set; }
    /// <summary>
    /// Mandatory field (row id)
    /// </summary>
    public string? RowKey { get; set; }
    /// <summary>
    /// Mandatory field (last modified)
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; } = default!;
    /// <summary>
    /// Mandatory field (unique id)
    /// </summary>
    public ETag ETag { get; set; } = default!;

    /// <summary>
    /// Custom table field
    /// </summary>
    public string? Value1 { get; set; } 
  }
```

## STEP 2: Create a concrete implementation of the class

In this class you specify the name of the table to store your "MyEntity". And you give base class the name of the ITableEntity to store in that table.

```csharp
using Azure.Data.Tables;

namespace MyCode
{
    public class MyRepository : AzureTableBaseRepository<MyEntity>
    {
        public SmsBatchesRepository(TableServiceClient tableServiceClient) : base("MyTable", tableServiceClient)
        {
        }
    }
}
```

## STEP 3: Setup dependency injection in Program.cs

```csharp
// Creating a connection to a storage account containing the 
// Azure Table
builder.Services.AddAzureClients(context => context.AddTableServiceClient(builder.Configuration.GetConnectionString("StorageAccount")));
// Allowing for depencency injection
builder.Services.AddTransient<MyRepository>();
```

# HOW TO USE THE REPOSITORY

## Injecting class into other class

```csharp
public class MyClass 
{
    private MyRepository _myRepository;

    public MyClass(MyRepository myRepository)
    {
        _myRepository = myRepository;
    }

    ...
    ...
}
```

## Create, Update, or Upsert 1 entity

```csharp
var entity = new MyEntity() 
{ 
    PartitionKey = "partition",
    RowKey = "row001",
    Value1 = "myvalue"
};

// Adds a new row. Will fail if the partitionkey/rowkey already exists
await _myRepository.AddAsync(entity);

// Updates a row. Will fail if the partitionkey/rowkey does not exist
await _myRepository.UpdateAsync(entity);

// If entity exists, updates the entity. If not, a new is created
await _myRepository.UpsertAsync(entity);
```

## Handle concurrency issues on update

You can use the UpdateAsync(T entity, ETag ifMatch, TableUpdateMode tableUpdateMode) method to handle concurrency issues:

```csharp
int maxRetries = 10;
int retryCount = 0;
while (retryCount < maxRetries)
{
    try
    {
        var entity = _myRepository.GetAsync("partition", "row001");
        entity.Value1 = entity.Value1 + "1";
        await _myRepository.UpdateAsync(entity, entity.ETag, TableUpdateMode.Replace);
        break;
    }
    // 412 = Precondition Failed (ETag mismatch)
    catch (RequestFailedException ex) when (ex.Status == 412) 
    {
        retryCount++;
        if (retryCount == maxRetries)
        {
            throw new Exception("Cannot update entity");
        }
        else
        {
            // Exponential backoff (0.5s, 1s, 1.5s, ...)
            await Task.Delay(500 * retryCount);
        }
    }
}
```

## Create, Update, or Upsert many entities

In these methods, you don't need to worry about the insert limits of Azure Tables, the class will chunk your array for you.

```csharp
List<MyEntity> entityList = new List<MyEntity>();

for (int i=0;i<100;i++)
{
    var entity = new MyEntity() 
    { 
        PartitionKey = "partition",
        RowKey = "row" + i,
        Value1 = "myvalue"
    };
}

// Adds new rows. Will fail if the partitionkeys/rowkeys already exists
await _myRepository.AddAsync(entityList);

// Updates rows. Will fail if the partitionkeys/rowkeys does not exist
await _myRepository.UpdateAsync(entityList);

// If entities exists, updates the entities. If not, new entities are created
await _myRepository.UpsertAsync(entityList);
```

## Read entities

```csharp
// One specific entity
var entity = await _myRepository.GetAsync("partition", "row001");

// Using LINQ to query table. Returns all entities matching the 
// query.
var entities = await _myRepository.GetAsync(e => e.PartitionKey == "partition");
```



