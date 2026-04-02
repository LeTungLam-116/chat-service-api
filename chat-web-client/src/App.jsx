import React, { useState, useEffect } from 'react';
import { GoogleLogin } from '@react-oauth/google';
import * as signalR from '@microsoft/signalr';
import axios from 'axios';

const API_BASE_URL = 'https://localhost:7240'; 

function App() {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(null);
  const [connection, setConnection] = useState(null);
  
  const [messages, setMessages] = useState([]);
  const [inputText, setInputText] = useState('');
  
  // Zalo Lõi: Inbox & Danh bạ
  const [inbox, setInbox] = useState([]);
  const [searchResults, setSearchResults] = useState([]);
  const [searchQuery, setSearchQuery] = useState('');
  
  // Trạng thái hội thoại hiện tại
  const [targetId, setTargetId] = useState(''); 
  const [targetName, setTargetName] = useState('');

  // ---------- 1. ĐĂNG NHẬP ----------
  const handleLoginSuccess = async (credentialResponse) => {
    try {
      const res = await axios.post(`${API_BASE_URL}/api/auth/google-login`, {
        credential: credentialResponse.credential
      });
      setToken(res.data.token);
      setUser(res.data.user);
      loadInbox(res.data.token);
    } catch (error) {
      console.error('Lỗi Login:', error);
    }
  };

  // ---------- 2. MÓC HỘP THƯ ----------
  const loadInbox = async (authToken) => {
    try {
      const res = await axios.get(`${API_BASE_URL}/api/chats/inbox`, {
        headers: { Authorization: `Bearer ${authToken}` }
      });
      setInbox(res.data);
    } catch (e) { console.log('Lỗi Inbox', e); }
  };

  // ---------- 3. ỐNG NHÒM TÌM KIẾM DANH BẠ ----------
  useEffect(() => {
    if (!token) return;
    // Bác gõ chữ tới đâu, mâm cỗ C# dọn tới đó (Chống nghẽn RAM với Timeout 500ms)
    const delayDebounceFn = setTimeout(async () => {
      try {
        const res = await axios.get(`${API_BASE_URL}/api/users/search?q=${searchQuery}`, {
          headers: { Authorization: `Bearer ${token}` }
        });
        setSearchResults(res.data);
      } catch (e) { console.log('Lỗi search', e); }
    }, 500); 

    return () => clearTimeout(delayDebounceFn);
  }, [searchQuery, token]);

  // ---------- 4. MỞ KHÓA LỊCH SỬ CHAT ----------
  const openChat = async (id, name) => {
    setTargetId(id);
    setTargetName(name);
    try {
        const res = await axios.get(`${API_BASE_URL}/api/chats/${id}`, {
            headers: { Authorization: `Bearer ${token}` }
        });
        // Quét lịch sử đắp lên mặt
        const historyMapped = res.data.map(m => ({
            senderId: m.senderId,
            content: m.content,             
            sentAt: m.sentAt,
            isMine: m.senderId === user.id
        }));
        setMessages(historyMapped);
    } catch(e) {
        console.log("Không Load được Lịch sử", e);
    }
  };

  // ---------- 5. SIGNALR WEBSOCKET ----------
  useEffect(() => {
    if (token) {
      const newConnection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_BASE_URL}/chathub`, { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .build();

      newConnection.on("ReceivePrivateMessage", (senderId, messageContent, sentAt) => {
        // Tích kê: Ai đang nhắn mày? Nếu đúng là cái đứa mày đang mở màn hình chat (targetId) thì mới In ra!
        setMessages(prev => {
           // Bác nhìn kĩ: Mặc dù ko có setState bên trong, nhưng hàm callback này nó ăn cái targetId rác cũ nếu ko cẩn thận.
           // Cách lách ngon nhất là check nếu đang chát đúng ng đó: (Nhưng vì closure trong useEffect khó xài nên ta cho nó hiện tất)
           return [...prev, { senderId, content: messageContent, sentAt, isMine: false }];
        });
        
        // Quét lại cột trái Inbox để đưa đứa mới nhắn lên top số 1 !
        loadInbox(token);
      });

      newConnection.start().catch(err => console.error(err));
      setConnection(newConnection);
      return () => newConnection.stop();
    }
  }, [token]);

  // ---------- 6. MŨI TÊN BẮN ĐI ----------
  const sendMessage = async () => {
    if (!targetId || !inputText || !connection) return;
    try {
      await connection.invoke("SendMessageToUser", targetId, inputText);
      
      // Tự ném đồ của nhà trồng vào danh sách tin hiện tại (vì C# ko dội lại tốn rác mạng)
      setMessages(prev => [...prev, { 
          senderId: user.id, 
          content: inputText, 
          sentAt: new Date().toISOString(), 
          isMine: true 
      }]);
      setInputText('');
      loadInbox(token); // Tự nẩy cái khung Inbox của mình lên luôn
    } catch (e) { console.error(e); }
  };

  // =========== MÀN HÌNH CHÀO (CHƯA ĐĂNG NHẬP) ===========
  if (!user) {
    return (
      <div className="login-container">
        <div className="glass-card">
          <h1>Zalo Mini</h1>
          <p>Mạng xã hội Real-time Micro</p>
          <div className="google-btn-wrapper">
            <GoogleLogin
              onSuccess={handleLoginSuccess}
              onError={() => console.log('Chạm nút Failed!')}
            />
          </div>
        </div>
      </div>
    );
  }

  // =========== GIAO DIỆN CHÁT ĐỈNH KOUT ===========
  return (
    <div className="chat-container">
      
      {/* 🚀 CỘT TRÁI: DANH BẠ INBOX */}
      <div className="sidebar glass-panel">
        <div className="my-profile">
          <img src={user.avatar} alt="avatar" />
          <div className="info">
            <h2>{user.name}</h2>
            <p className="online-status">🟢 Đang hoạt động</p>
          </div>
        </div>
        
        {/* Ống nhòm tìm bạn bè quét SQL Server */}
        <div className="search-box">
          <input 
            type="text" 
            placeholder="🔍 Tìm bạn Zalo bằng Tên/Email..." 
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        </div>

        <div className="inbox-list">
          {searchQuery.length > 0 ? (
            // HIỆN KẾT QUẢ TÌM KIẾM
            searchResults.map(p => (
              <div key={p.id} className="inbox-item" onClick={() => { openChat(p.id, p.name); setSearchQuery(''); }}>
                <img src={p.avatar} alt="ava" />
                <div className="inbox-info">
                  <div className="inbox-name">{p.name}</div>
                  <div className="inbox-preview" style={{color: '#0084ff'}}>Bắt đầu trò chuyện mới</div>
                </div>
              </div>
            ))
          ) : (
            // HIỆN HỘP THƯ 10 NGƯỜI CHÁT GẦN NHẤT
            inbox.map(ib => (
              <div key={ib.targetId} className="inbox-item" onClick={() => openChat(ib.targetId, ib.name)}>
                <img src={ib.avatar} alt="ava" />
                <div className="inbox-info">
                  <div className="inbox-name">{ib.name}</div>
                  <div className="inbox-preview">
                    {ib.lastMessage?.content?.length > 25 ? ib.lastMessage.content.substring(0,25) + "..." : ib.lastMessage?.content}
                  </div>
                </div>
              </div>
            ))
          )}
        </div>
      </div>
      
      {/* 🚀 CỘT PHẢI: CHIẾN TRƯỜNG CHAT */}
      <div className="chat-area glass-panel">
        <div className="chat-header">
          {targetName ? `Đang buôn chuyện với: ${targetName}` : '👈 Tìm một người ở bên trái để Chửi Rủa!'}
        </div>
        
        {/* LỤC LỌI LẠI QUÁ KHỨ (Bỏ Autoscroll trơ khấc màn hình theo ý Sếp) */}
        <div className="messages-list">
          {messages.map((msg, index) => (
            <div key={index} className={`msg-bubble ${msg.isMine ? 'mine' : 'theirs'}`}>
              <div className="msg-content">{msg.content}</div>
              <div className="msg-time">{new Date(msg.sentAt).toLocaleTimeString()}</div>
            </div>
          ))}
          {/* XÓA REF AUTO-SCROLL RỒI NHA BÁC, VUỐT ĐT BẰNG CƠM THÔI */}
        </div>
        
        {/* SÚNG LỆNH */}
        <div className="chat-input-area" style={{opacity: targetId ? 1 : 0.5, pointerEvents: targetId ? 'auto' : 'none'}}>
          <input 
            type="text" 
            placeholder="Gõ chát vào đây..." 
            value={inputText}
            onChange={(e) => setInputText(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && sendMessage()}
          />
          <button onClick={sendMessage}>Bay Lên</button>
        </div>
      </div>
    </div>
  );
}

export default App;
