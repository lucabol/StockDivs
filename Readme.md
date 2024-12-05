# Example of using Docker Compose or Aspire to create a simple microservice-based application

You can run the application locally using Docker Compose or Aspire:
* `docker-compose up --build`
* `dotnet run --project Apphost/Apphost.csproj`

You can deploy it to Azure using either:
* `.\ContainerApp-UpCompose.ps1`
* `azd up` - if you are on windows run this command from the provided devcontainer
