﻿'NetCore v1.1
'Based on https://msdn.microsoft.com/en-us/library/aa478452.aspx
'
'---------------
'
'About:
'
'NetServer: A class that allows you to easily create and manage a socket listener
'NetClientObj: A class created by NetServer that represents an individual client connected to the server. Can be used to send messages to thay client. Only to be used on the server side.
'NetClient: A class that allows you to easily create and manage a socket client
'
'---------------
'
'NetServer:
'Usage-
'server.StartListener(ByVal port As Int32) - Starts the server
'server.StopListener() - Stops the server
'server.tblClients - Hash table of clients by GUID
'
'Sending Messages-
''client is an instance of NetClientObj that is generated by NetServer
'client.ID - The client GUID
'client.Send(ByVal data As String) - Sends data to the client
'
'Events-
'evtClientConnected(ByVal client As NetClientObj) - New client has connected
'evtClientDisconnected(ByVal client As NetClientObj) - Client has disconnected
'evtReceived(ByVal client As NetClientObj, ByVal data As String) - Client has sent a message
'
'Example-
'Dim server = New NetServer() 'Create a new server object
'
''Assign callbacks to events
'Private Sub OnConnected(ByVal client As NetClient)
'    client.send("Hello " & client.ID & "!") 'Send the client a welcome message
'    ...
'End Sub
'
'Private Sub OnDisConnected(ByVal client As NetClient)
'    ...
'End Sub
'
'Private Sub OnLineReceived(ByVal client As NetClient, ByVal data As String)
'    ...
'End Sub
'
'AddHandler server.evtClientConnected, AddressOf OnConnected 
'AddHandler server.evtClientDisconnected, AddressOf OnDisconnected
'AddHandler server.evtRecieved, AddressOf OnLineReceived
'
'...
'
'server.StopListener()
'
'---------------
'
'NetClient:
'
'Usage-
'client = New NetClient(ByVal ip As String, ByVal port As Int32) - Connects to a server
'client.StopConnection() - Closes the connection with the server
'client.SendMessage(ByVal data As String) - Sends data to the server
'
'Events-
'evtDisconnected() - Triggered when the connection to the server is disconnected
'evtReceived(ByVal data As String) - Triggered data is received from the server
'
'Example-
'Dim client = New NetClient("127.0.0.1", "8080") 'Connects to the server at 127.0.0.1:8080
'
''Assign callbacks to events
'Private Sub OnDisConnected()
'    ...
'End Sub
'
'Private Sub OnLineReceived(ByVal data As String)
'    ...
'End Sub
'
'AddHandler server.evtDisconnected, AddressOf OnDisconnected
'AddHandler server.evtRecieved, AddressOf OnLineReceived
'
'client.SendMessage("Hello Server!") 'Sends a message to the server
'
'client.StopConnection() 'Closes the connection with the server

Imports System.Net.Sockets
Imports System.Text

Public Class NetClientObj
    Public Event evtConnected(ByVal nctSender As NetClientObj)
    Public Event evtDisconnected(ByVal nctSender As NetClientObj)
    Public Event evtReceived(ByVal nctSender As NetClientObj, ByVal strData As String)

    Private gidClient As Guid = Guid.NewGuid
    Private tcpClientConnection As TcpClient
    Private bytData(1024) As Byte
    Private bldText As New StringBuilder()

    Public ReadOnly Property ID() As String
        Get
            Return gidClient.ToString
        End Get
    End Property

    Public Sub New(ByVal tcpConnection As TcpClient)
        tcpClientConnection = tcpConnection
        RaiseEvent evtConnected(Me)
        tcpClientConnection.GetStream.BeginRead(bytData, 0, 1024, _
          AddressOf ReceiveLoop, Nothing)
    End Sub

    Private Sub ReceiveLoop(ByVal ar As IAsyncResult)
        Dim intCount As Integer

        Try
            SyncLock tcpClientConnection.GetStream
                intCount = tcpClientConnection.GetStream.EndRead(ar)
            End SyncLock
            If intCount < 1 Then
                RaiseEvent evtDisconnected(Me)
                Exit Sub
            End If

            BuildString(bytData, 0, intCount)

            SyncLock tcpClientConnection.GetStream
                tcpClientConnection.GetStream.BeginRead(bytData, 0, 1024, _
                  AddressOf ReceiveLoop, Nothing)
            End SyncLock
        Catch e As Exception
            RaiseEvent evtDisconnected(Me)
        End Try
    End Sub

    Private Sub BuildString(ByVal bytBytes() As Byte, _
      ByVal intOffset As Integer, ByVal intCount As Integer)
        Dim intIndex As Integer

        For intIndex = intOffset To intOffset + intCount - 1
            If bytBytes(intIndex) = 13 Then
                RaiseEvent evtReceived(Me, bldText.ToString)
                bldText = New StringBuilder()
            Else
                bldText.Append(ChrW(bytBytes(intIndex)))
            End If
        Next
    End Sub

    Public Sub Send(ByVal strData As String)
        SyncLock tcpClientConnection.GetStream
            Dim w As New IO.StreamWriter(tcpClientConnection.GetStream)
            w.Write(strData & vbCrLf)
            w.Flush()
        End SyncLock
    End Sub
End Class

Public Class NetServer
    Public tblClients As New Hashtable()
    Private tcpSeverListener As TcpListener
    Private thdConnections
    Private intConnectionPort As Int32
    Public Event evtClientConnected(ByVal nctClient As NetClientObj)
    Public Event evtClientDisconnected(ByVal nctClient As NetClientObj)
    Public Event evtReceived(ByVal nctClient As NetClientObj, ByVal strData As String)

    Public Sub StartListener(ByVal intPort As Int32)
        intConnectionPort = intPort
        thdConnections = New Threading.Thread(AddressOf ReceiveLoop)
        thdConnections.Start()
    End Sub

    Public Sub StopListener()
        tcpSeverListener.Stop()
    End Sub

    Private Sub ReceiveLoop()
        Try
            tcpSeverListener = New TcpListener(System.Net.IPAddress.Parse("127.0.0.1"), intConnectionPort)
            tcpSeverListener.Start()
            Do
                Dim x As New NetClientObj(tcpSeverListener.AcceptTcpClient)

                AddHandler x.evtConnected, AddressOf OnConnected
                AddHandler x.evtDisconnected, AddressOf OnDisconnected
                AddHandler x.evtReceived, AddressOf OnLineReceived
                tblClients.Add(x.ID, x)

                RaiseEvent evtClientConnected(x)
            Loop Until False
        Catch
        End Try
    End Sub

    Private Sub OnConnected(ByVal nctSender As NetClientObj)
        RaiseEvent evtClientConnected(nctSender)
    End Sub

    Private Sub OnDisconnected(ByVal nctSender As NetClientObj)
        RaiseEvent evtClientDisconnected(nctSender)
        tblClients.Remove(nctSender.ID)
    End Sub

    Private Sub OnLineReceived(ByVal nctSender As NetClientObj, ByVal strData As String)
        RaiseEvent evtReceived(nctSender, strData)
    End Sub
End Class

Public Class NetClient
    Private tcpClientConnection As TcpClient
    Private bytData(1024) As Byte
    Private bldText As New StringBuilder()
    Public Event evtDisconnected()
    Public Event evtReceived(ByVal strData As String)

    Public Sub New(ByVal strIP As String, ByVal intPort As Int32)
        Try
            tcpClientConnection = New TcpClient(strIP, intPort)
            tcpClientConnection.GetStream.BeginRead(bytData, 0, 1024, _
              AddressOf ReceiveLoop, Nothing)
        Catch
            RaiseEvent evtDisconnected()
        End Try
    End Sub

    Public Sub StopConnection()
        tcpClientConnection.Close()
    End Sub

    Private Sub Send(ByVal strData As String)
        Dim w As New IO.StreamWriter(tcpClientConnection.GetStream)
        w.Write(strData & vbCr)
        w.Flush()
    End Sub

    Public Sub SendMessage(ByVal strData As String)
        Me.Send(strData)
    End Sub

    Private Sub ReceiveLoop(ByVal ar As IAsyncResult)
        Dim intCount As Integer

        Try
            intCount = tcpClientConnection.GetStream.EndRead(ar)
            If intCount < 1 Then
                RaiseEvent evtDisconnected()
                Exit Sub
            End If

            BuildString(bytData, 0, intCount)

            tcpClientConnection.GetStream.BeginRead(bytData, 0, 1024, _
              AddressOf ReceiveLoop, Nothing)
        Catch e As Exception
            RaiseEvent evtDisconnected()
        End Try
    End Sub

    Private Sub BuildString(ByVal bytBytes() As Byte, _
      ByVal intOffset As Integer, ByVal intCount As Integer)
        Dim intIndex As Integer

        For intIndex = intOffset To intOffset + intCount - 1
            If bytBytes(intIndex) = 10 Then
                bldText.Append(vbLf)

                RaiseEvent evtReceived(bldText.ToString)

                bldText = New StringBuilder()
            Else
                bldText.Append(ChrW(bytBytes(intIndex)))
            End If
        Next
    End Sub
End Class