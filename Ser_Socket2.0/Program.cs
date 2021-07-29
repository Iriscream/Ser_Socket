using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;
using System.IO;

namespace Ser_Socket2._0
{
    class Ser_Socket
    {
        Socket mySocket = null;
        Dictionary<IPAddress, Socket> cliDic = new Dictionary<IPAddress, Socket>();
        public struct Infor
        {
            public string passWord;
            public int score;
        }
        Dictionary<string, Infor> CliInf = new Dictionary<string, Infor>();
        //记录用户的id,密码和积分
        FileStream cliFile;
        StreamReader fileReader;
        StreamWriter fileWriter;
        public struct player
        {
            public Socket clisoket;
            public string id;
            public bool Checkin;
        }
        public class players
        {
            public player player1;
            public player player2;
            public players(player cliplayer1, player cliplayer2)
            {
                player1 = cliplayer1;
                player2 = cliplayer2;
            }
        }
        public player cliplayer1;
        public player cliplayer2;
        players Players;
        //一次两位客户端的请求
        int mark = 0;
        //记录加入人数
        //固定选手
        //建立用户与服务器的联系


        //开始运行并尝试建立连接
        public void Connect(int port)
        {
            //string IP = "192.168.1.101"; //寝室1
            string IP = "10.30.41.206";  //校园
            //string IP="192.168.5.143";  //寝室2
            //IPAddress IPAddress = IPAddress.Parse("10.30.41.206");
            IPAddress address = IPAddress.Any;
            //创建IP终结点，把IP地址与端口绑定到网络终结点上
            IPEndPoint endPoint = new IPEndPoint(address, port);
            //创建客户端套接字
            mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ///监听套接字终结点
            mySocket.Bind(endPoint);
            //服务端可接收客户端连接数量为无限个
            mySocket.Listen(0);
            //开启线程监听客户端
            Thread myThread = new Thread(Listen_Con);
            myThread.Start();
            Console.WriteLine("开始与用户建立连接...");
        }
        //接收客户端，并两两配对
        public void Listen_Con(Object obj)
        {
            ReadCliFile();
            while (true)
            {
                Socket cliSocket = null;
                //规定一次只可加入两位
                try
                {
                    cliSocket = mySocket.Accept();
                    //看是否有用户此刻请求加入
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                string cliEndPoint = cliSocket.RemoteEndPoint.ToString();
                IPAddress cliAddress = (cliSocket.RemoteEndPoint as IPEndPoint).Address;
                int cliPort = (cliSocket.RemoteEndPoint as IPEndPoint).Port;
                //客户端信息转换
                if (!cliDic.ContainsKey(cliAddress))
                {
                    cliDic.Add(cliAddress, cliSocket);
                }

                //客户端的信息记录
                string MsgStr = "[客户端结点:" + cliEndPoint + "\n+客户端IP：" +
                                        cliAddress.ToString() + "\n客户端端口:" +
                                            cliPort.ToString() + "\n已连接]";
                Console.WriteLine(MsgStr);
                player cliplayer = new player();
                cliplayer.clisoket = cliSocket;
                Thread threadMain = new Thread(MainMenu);
                threadMain.Start(cliplayer);
            }
        }
        //待机状态 判断选择项目
        //1.人机 过程不参与，但游戏结束需更新积分
        //2.联网 过程全程参与，并有过程中的按钮监听
        //3.退出（联网游戏退出）保持继续监听模式(同时监听两个的内容)
        //4.登陆或注册

        public void MainMenu(object player)
        {
            player client = (player)player;
            string msg;
            while (true)
            {
                msg = RecvCli(client.clisoket);
                if (msg == "LOGIN" || msg == "SIGNIN")
                {
                    client = Checkin(client.clisoket, msg);//点击
                }
                if (msg == "SCORE")
                {
                    UpDateScore(client);
                }
                if (msg == "ONLINE")
                {
                    MakeTeam(client);
                    break;
                }
                if (msg == "RANK")
                {
                    SortRank(client);
                }
                if (msg == "MYRANK")
                {
                    SendCli(client.clisoket, Convert.ToString(CliRank(client.id)));
                }
                if (msg == null)
                    break;

            }
        }
        //人机对战，只接收到最后成绩即可
        public void UpDateScore(player player)
        {
            SendCli(player.clisoket, "TRUE");
            //提醒已接收到更新成绩指令，现在只需获取最终成绩即可
            Infor infor = new Infor();
            infor.score = Convert.ToInt32(RecvCli(player.clisoket));
            infor.passWord = CliInf[player.id].passWord;
            CliInf[player.id] = infor;
            SendCli(player.clisoket, "TRUE");
            WriteAllFile();
        }


        //联网对战
        //step1:cli->ONLINE会收到FIRST(白棋，先下)/SECOND(黑棋，后下),均恢复TREU->Ser
        //step2:！！！获取对手信息,id->Cli,TRUE->Ser,Score->Cli,TRUE->Ser,Rank->Cli,TRUE->Ser,
        //step3:cli1<-MOVE 开始下棋 并将下棋的信息->Ser Ser->cli2 cli2->Ser Ser->cli1.....开始循环
        //step4:如果在发出投降，另一方直接胜利，该方失败
        //step5:如果选择举报,会对面用户的积分进行处罚，并提醒对面被举报

        //每进入两人就进行联网和配对
        public void MakeTeam(player player)
        {
            player cliplayer = player;
            if (mark >= 2)
            {
                cliplayer1 = new player();
                cliplayer2 = new player();
                mark = 0;
            }
            if (mark == 0)
            {
                Console.WriteLine("用户{0}进入\n", mark + 1);
                cliplayer1 = cliplayer;    //先进行用户的登陆和注册再配对
                SendCli(cliplayer.clisoket, "FIRST");
                RecvCli(cliplayer.clisoket);
                mark++;
                return;
            }
            else if (mark == 1)
            {
                Console.WriteLine("用户{0}进入\n", mark + 1);
                cliplayer2 = cliplayer;
                SendCli(cliplayer.clisoket, "SCEOND");
                RecvCli(cliplayer.clisoket);
                SendCli(cliplayer1.clisoket, "MAKE!");
                if (RecvCli(cliplayer1.clisoket) == "TRUE")
                {
                    Players = new players(cliplayer1, cliplayer2);
                    InformChang(cliplayer1, cliplayer2);
                    InformChang(cliplayer2, cliplayer1);
                    Thread TeamThread = new Thread(GameBegin);
                    TeamThread.Start(Players);
                }
                mark++;
            }
        }

        //发送对手信息  player2->player1
        public void InformChang(player player1, player player2)
        {
            SendCli(player1.clisoket, player2.id);
            RecvCli(player1.clisoket);
            SendCli(player1.clisoket, Convert.ToString(CliInf[player2.id].score));
            RecvCli(player1.clisoket);
            SendCli(player1.clisoket, Convert.ToString(CliRank(player2.id)));
            RecvCli(player1.clisoket);
        }

        //联网状态信息交流
        public void GameBegin(object players)
        {
            players cliplayers = (players)players;
            //三种信息交流：1.玩家出棋（时间限制然后人机出棋？）；2.玩家投降；3.重开一局
            string play1_msg, play2_msg;
            int get=2;
            SendCli(cliplayers.player1.clisoket, "MOVE");
            //新进来者先动
            while (true)
            {
                play1_msg = RecvCli(cliplayers.player1.clisoket);//收到信息
                if (play1_msg == "OUT" && get == 1)
                //退出联网模式
                {
                    SendCli(cliplayers.player1.clisoket, "TRUE");
                    Thread threadMain = new Thread(MainMenu);
                    threadMain.Start(cliplayers.player1);
                    get--;
                    if (get == 0)
                        break;
                }
                SendCli(cliplayers.player2.clisoket, play1_msg);//给后方
                if (play1_msg == "OUT"&&get==2)
                //退出联网模式或者为投降
                {
                    SendCli(cliplayers.player1.clisoket, "TRUE");
                    Thread threadMain = new Thread(MainMenu);
                    threadMain.Start(cliplayers.player1);
                    get--;
                }

                play2_msg = RecvCli(cliplayers.player2.clisoket);
                if (play2_msg == "OUT"&&get==1)
                //退出联网模式
                {
                    SendCli(cliplayers.player2.clisoket, "TRUE");
                    Thread threadMain = new Thread(MainMenu);
                    threadMain.Start(cliplayers.player2);
                    get--;
                    if (get == 0)
                        break;
                }
                SendCli(cliplayers.player1.clisoket, play2_msg);
                if (play2_msg == "OUT"&&get==2)
                //退出联网模式
                {
                    SendCli(cliplayers.player2.clisoket, "TRUE");
                    Thread threadMain = new Thread(MainMenu);
                    threadMain.Start(cliplayers.player2);
                    get--;
                   
                }

            }
        }

        //信息初始更新：注册or登陆
        public player Checkin(Socket cliSocket, string recstr)
        {
            player Player = new player();
            if (recstr == "LOGIN")
            {
                SendCli(cliSocket, "TRUE");
                Player = Check_login(cliSocket);
            }
            else if (recstr == "SIGNIN")
            {
                SendCli(cliSocket, "TRUE");
                Player = Check_signin(cliSocket);
            }
            return Player;
        }

        //排名
        public void SortRank(player player)
        {
            var sortResult = from pair in CliInf orderby pair.Value.score descending select pair; //以字典Value的引用值逆序排序
            //只传入前十
            int rankNum = 1;
            foreach (KeyValuePair<string, Infor> i in sortResult)
            {
                SendCli(player.clisoket, "TRUE");
                if (RecvCli(player.clisoket) == "GET")
                    SendCli(player.clisoket, Convert.ToString(rankNum));        //传入排名
                if (RecvCli(player.clisoket) == "GET")
                    SendCli(player.clisoket, i.Key);                            //传入id
                if (RecvCli(player.clisoket) == "GET")
                    SendCli(player.clisoket, Convert.ToString(i.Value.score));  //传入积分
                if (RecvCli(player.clisoket) == "GET")
                {
                    if (rankNum == 10)
                        break;
                    rankNum++;
                }
            }
            SendCli(player.clisoket, "FALSE");
        }

        //获取个人排名
        public int CliRank(string id)
        {
            var sortResult = from pair in CliInf orderby pair.Value.score descending select pair; //以字典Value的引用值逆序排序
            int rankNum = 1;
            foreach (KeyValuePair<string, Infor> i in sortResult)
            {
                if (i.Key == id)
                    return rankNum;
                rankNum++;
            }
            return 0;
        }
        //用户注册
        public player Check_signin(Socket client)
        {
            Console.WriteLine("进入注册");
            player Player = new player();
            Player.clisoket = client;
            string tempID = RecvCli(client);
            if (CliInf.ContainsKey(tempID))
            {
                Player.Checkin = false;
                SendCli(client, "FALSE");
                return Player;
            }//该用户存在
            Player.id = tempID;
            SendCli(client, "TRUE");
            //接收id
            Infor infor = new Infor();
            infor.score = 50;
            infor.passWord = RecvCli(client);
            SendCli(client, "TRUE");
            CliInf.Add(Player.id, infor);
            WriteAllFile();
            //存入密码
            //告知客户端存入成功
            Player.Checkin = true;
            return Player;
            //返回选手信息
        }

        //用户登陆
        public player Check_login(Socket client)
        {
            Console.WriteLine("进入登陆：");
            player Player = new player();
            Player.clisoket = client;
            string tempID = RecvCli(client);
            //判断id是否存在
            if (!CliInf.ContainsKey(tempID))
            {
                //id不存在
                Player.Checkin = false;
                SendCli(client, "FALSE");
                return Player;
            }
            //id存在
            Player.id = tempID;
            SendCli(client, "TRUE");
            //判断密码是否正确
            if (!(CliInf[Player.id].passWord == RecvCli(client)))
            {
                Player.Checkin = false;
                //密码错误
                SendCli(client, "FALSE");
                return Player;
            }
            //密码正确
            SendCli(client, "TRUE");
            if (RecvCli(client) == "SCORE")
            {
                SendCli(client, Convert.ToString(CliInf[Player.id].score));
            }
            Player.Checkin = true;
            return Player;
        }

        //发送客户端信息
        public void SendCli(Socket client, string Msgstr)
        {
            byte[] ByteToAll = new byte[1024 * 1024];
            try
            {
                ByteToAll = Encoding.UTF8.GetBytes(Msgstr);
                client.Send(ByteToAll);
                Console.WriteLine(Msgstr);
            }
            catch (Exception)
            {
                Console.WriteLine("ERROR:" + client.RemoteEndPoint + "已与服务端断开！");
                client.Close();
                if (cliDic.ContainsKey((client.RemoteEndPoint as IPEndPoint).Address))
                {
                    cliDic.Remove((client.RemoteEndPoint as IPEndPoint).Address);
                }
            }
        }

        //接收来自客户端的消息
        public string RecvCli(Socket clisocket)
        {
            byte[] recBytes = new byte[1024 * 1024];
            //尝试把接收的字节存储到缓冲区
            try
            {
                int length = clisocket.Receive(recBytes);
                string recMsg = Encoding.UTF8.GetString(recBytes, 0, length);
                Console.WriteLine(recMsg);
                return recMsg;
            }
            catch (Exception)
            {
                cliDic.Remove((clisocket.RemoteEndPoint as IPEndPoint).Address);
                //客户端断开的异常
                Console.WriteLine("[客户端" + (clisocket.RemoteEndPoint as IPEndPoint).Address + "已断开]");
                Console.WriteLine("[客户端终结点：" + clisocket.RemoteEndPoint + "]");
                //断开套接字
                clisocket.Close();
                return null;
            }
        }

        //读取客户端信息文件
        public void ReadCliFile()
        {
            try
            {
                cliFile = new FileStream(@"..\..\ClientInform.txt", FileMode.Open);
                fileReader = new StreamReader(cliFile);
                Console.WriteLine("Open File!");
            }
            catch (IOException e)
            {
                Console.WriteLine("Cannot OPEN the file!");
            }
            string id;
            Infor infor = new Infor();
            while (!fileReader.EndOfStream)
            {
                id = fileReader.ReadLine();
                infor.passWord = fileReader.ReadLine();
                infor.score = Convert.ToInt32(fileReader.ReadLine());
                CliInf.Add(id, infor);
            }
            cliFile.Close();
        }

        //添加用户信息到文件中
        public void WriteCliFile(string id, Infor infor)
        {
            try
            {
                cliFile = new FileStream(@"..\..\ClientInform.txt", FileMode.Append);
                fileWriter = new StreamWriter(cliFile);
            }
            catch (IOException e)
            {
                Console.WriteLine("Cannot OPEN the file!");
            }
            fileWriter.WriteLine(id);
            fileWriter.WriteLine(infor.passWord);
            fileWriter.WriteLine(Convert.ToString(infor.score));
            fileWriter.Flush();
            fileWriter.Close();
            cliFile.Close();
        }

        //将用户信息数据再加入到文件中
        public void WriteAllFile()
        {
            try
            {
                cliFile = new FileStream(@"..\..\ClientInform.txt", FileMode.Create);
                fileWriter = new StreamWriter(cliFile);
            }
            catch (IOException e)
            {
                Console.WriteLine("Cannot OPEN the file!");
            }
            foreach (KeyValuePair<string, Infor> i in CliInf)
            {
                fileWriter.WriteLine(i.Key);
                fileWriter.WriteLine(i.Value.passWord);
                fileWriter.WriteLine(Convert.ToString(i.Value.score));
                fileWriter.Flush();
            }
            fileWriter.Close();
            cliFile.Close();

        }
    }
    class ServerMain
    {
        static void Main(string[] args)
        {
            Ser_Socket s1 = new Ser_Socket();
            s1.Connect(8800);
        }
    }

}
