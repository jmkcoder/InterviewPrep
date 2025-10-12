# GraphQL Support for Employee API

## Overview
Your Employee API now supports GraphQL queries through the `/graphql` endpoint. This allows clients to request exactly the data they need with a single request.

## GraphQL Endpoint
Based on your `launchSettings.json`, the GraphQL endpoint is available at:
- **HTTP**: `http://localhost:5134/graphql`
- **HTTPS**: `https://localhost:7103/graphql`
- **Method**: POST (for queries) or GET (with query parameters)

## Banana Cake Pop UI
HotChocolate provides a built-in GraphQL IDE called Banana Cake Pop. 

**To access it:**
1. Start your application: `dotnet run`
2. Open your browser and navigate to: `http://localhost:5134/graphql`
3. The Banana Cake Pop interface will load automatically

## Available Queries

### 1. Get All Employees
Retrieves all employees from the database.

```graphql
query {
  employees {
    employeeNo
    name
    job
    manager
    hireDate
    salary
    commission
    departmentNo
  }
}
```

### 2. Get Employee By ID
Retrieves a specific employee by their employee number.

```graphql
query {
  employeeById(employeeNo: 7369) {
    employeeNo
    name
    job
    manager
    hireDate
    salary
    commission
    departmentNo
  }
}
```

### 3. Get Employees By Department
Retrieves employees filtered by department (optional filter).

```graphql
query {
  employeesByDepartment(departmentId: 10) {
    employeeNo
    name
    job
    salary
    departmentNo
  }
}
```

Or get all employees without department filter:

```graphql
query {
  employeesByDepartment {
    employeeNo
    name
    job
    salary
    departmentNo
  }
}
```

## Field Selection
With GraphQL, you can select only the fields you need:

```graphql
query {
  employees {
    name
    salary
  }
}
```

This will only return the name and salary for each employee, reducing payload size.

## Query Variables
You can use variables in your queries:

```graphql
query GetEmployee($empNo: Int!) {
  employeeById(employeeNo: $empNo) {
    employeeNo
    name
    job
    salary
  }
}
```

Variables:
```json
{
  "empNo": 7369
}
```

## Benefits of GraphQL

1. **Flexible Queries**: Request exactly the data you need
2. **Single Endpoint**: All queries go through `/graphql`
3. **Type Safety**: Strong typing with schema validation
4. **Self-Documenting**: Schema introspection provides built-in documentation
5. **Reduced Over-fetching**: Only requested fields are returned
6. **Batch Requests**: Multiple queries in a single HTTP request

## Integration with Existing Services
The GraphQL implementation uses your existing:
- `IGetEmployeesUsecase`
- `IGetEmployeeByIdUsecase`
- `IGetEmployeesStreamUsecase`

No changes were made to the REST API - both REST and GraphQL endpoints work side by side.

## Testing
You can test the GraphQL endpoint using:
1. **Banana Cake Pop** (built-in UI at `/graphql`)
2. **Postman** (GraphQL request type)
3. **cURL**:
   ```bash
   curl -X POST https://localhost:<port>/graphql \
     -H "Content-Type: application/json" \
     -d '{"query":"{ employees { name salary } }"}'
   ```
4. **Any GraphQL client library** (Apollo, Relay, etc.)
