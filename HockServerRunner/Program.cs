var server = new HQMServer();
server.onLog += Server_onLog;
server.RunServer(27777, new HQMServerConfiguration());

void Server_onLog(string obj)
{
    Console.WriteLine(obj);
}
