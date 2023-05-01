# Custom-Database-Server

A simple and inefficient database server with a custom query language.

## Description

The query examples for the language are in the section at the bottom of this page. The language supports `CREATE`, `ADD`, `RETRIEVE`, `GET`, `DROP`, `REMOVE` and `REPLACE` queries. `GET` queries can have optional predicates (analogus to WHERE clause in SQL) for selection.  

You can create custom entities (SQL tables) with specified types. Apart from primitives, arrays and pointers are also supported types. A pointer is the unique identifier of an object. Every object has a "pointer" column by default, which serves as a primary key. You can use pointers to other types as foreign keys. Pointers can also be used for direct interaction with the database (specifically, for `RETRIEVE`/`REMOVE`/`REPLACE` queries).

## Getting Started

### Dependencies

* [dotnet7](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

### Installing

* Run `dotnet restore` to install the dependencies
* Run `dotnet build --configuration Release --no-restore` to build

### Executing program

* Run `dotnet run --project CustomDatabase` to execute. Specify the port in `./CustomDatabase/Properties/launchSettings`.
* Alternatively, build a Docker container using `docker build -t custom-db .`, run with `docker run -p 5005:80 custom-db`. Instead of 5005 you can specify some other port.
* When the server started working, to execute queries, you can send GET HTTP requests on `{{DATABASE_URL}}/Query?query={{SOME_URI_ENCODED_QUERY}}`

### Query examples
Creating some entity.
```
Query: create entity Timer {name: string, start_time: string, duration_seconds: int}
Output example 1: { "Ok": "Successful!" }
Output example 2: { "Error": "Entity name already defined!"}
```

Adding instances and getting pointers to the newly created instances.
```
Query: add [Timer] 
    [{"start_time":"18:00 idk", "name":"my timer!", "duration_seconds":39}, 
    {"start_time":"13:37 idk", "name":"my timer2!", "duration_seconds":69}]
Output example: { "Ok": [ "timer1","timer2"]}
```

Conditional instance selection.
```
Query: get [Timer] where duration_seconds = 69
Output example: { "Ok": [
        {
            "pointer": "timer2",
            "name": "my timer2!",
            "start_time": "13:37 idk",
            "duration_seconds": 69
        } ]
    }
```


## License

This project is licensed under the MIT License - see the `LICENSE.md` file for details.

