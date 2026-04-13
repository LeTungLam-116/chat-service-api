import React, { useState, useEffect, useRef } from 'react';
import { GoogleLogin } from '@react-oauth/google';
import * as signalR from '@microsoft/signalr';
import axios from 'axios';

const API_BASE_URL = 'https://localhost:7240'; 

function App() {
  const [user, setUser] = useState(() => {
    const saved = localStorage.getItem('chatUser');
    return saved ? JSON.parse(saved) : null;
  });
  const [token, setToken] = useState(() => localStorage.getItem('chatToken'));
  const [connection, setConnection] = useState(null);
  
  const [messages, setMessages] = useState([]);
  const [inputText, setInputText] = useState('');
  
  // Tab Navigation
  const [activeTab, setActiveTab] = useState('messages'); // 'messages' | 'contacts'

  // Inbox State
  const [inbox, setInbox] = useState([]);
  const [searchResults, setSearchResults] = useState([]);
  const [searchQuery, setSearchQuery] = useState('');
  
  // Contacts State
  const [friends, setFriends] = useState([]);
  const [pendingRequests, setPendingRequests] = useState([]);
  const [myGroups, setMyGroups] = useState([]);
  const [pendingGroupRequests, setPendingGroupRequests] = useState([]);

  // Active Chat State
  const [targetId, setTargetId] = useState(''); 
  const [targetName, setTargetName] = useState('');
  const [chatType, setChatType] = useState('direct'); // direct | group
  const [currentFriendStatus, setCurrentFriendStatus] = useState(1); // 1: Friend, -1: Stranger
  
  const targetIdRef = useRef('');
  targetIdRef.current = targetId;
  const userRef = useRef(null);
  userRef.current = user;

  const [typingUsers, setTypingUsers] = useState({});

  // ---------- 1. LOGIN / LOGOUT ----------
  const handleLoginSuccess = async (credentialResponse) => {
    try {
      const res = await axios.post(`${API_BASE_URL}/api/auth/google-login`, {
        credential: credentialResponse.credential
      });
      setToken(res.data.token);
      setUser(res.data.user);
      localStorage.setItem('chatToken', res.data.token);
      localStorage.setItem('chatUser', JSON.stringify(res.data.user));
    } catch (error) { console.error('Lỗi Login:', error); }
  };

  const handleLogout = () => {
      setToken(null);
      setUser(null);
      setMessages([]);
      setInbox([]);
      localStorage.removeItem('chatToken');
      localStorage.removeItem('chatUser');
      if (connection) connection.stop();
  };

  // ---------- 2. VẬN HÀNH DỮ LIỆU ĐỊNH KỲ ----------
  const loadInbox = async () => {
    if (!token) return;
    try {
      const res = await axios.get(`${API_BASE_URL}/api/chats/inbox`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setInbox(res.data);
    } catch (e) { console.log('Lỗi Inbox', e); }
  };

  const loadContacts = async () => {
    if (!token) return;
    try {
      const preq = await axios.get(`${API_BASE_URL}/api/friends/pending`, { headers: { Authorization: `Bearer ${token}` }});
      setPendingRequests(preq.data);
      
      const pfr = await axios.get(`${API_BASE_URL}/api/friends`, { headers: { Authorization: `Bearer ${token}` }});
      setFriends(pfr.data);

      const pgr = await axios.get(`${API_BASE_URL}/api/groups/my`, { headers: { Authorization: `Bearer ${token}` }});
      setMyGroups(pgr.data);

      let pgrReqs = [];
      for (const gr of pgr.data) {
          if (gr.isAdmin) {
              const dr = await axios.get(`${API_BASE_URL}/api/groups/${gr.id}/requests`, { headers: { Authorization: `Bearer ${token}` }});
              pgrReqs = [...pgrReqs, ...dr.data.map(req => ({ ...req, groupId: gr.id, groupName: gr.name }))];
          }
      }
      setPendingGroupRequests(pgrReqs);
    } catch(e) { console.log('Lỗi Danh Bạ', e); }
  }

  useEffect(() => {
      if (token) {
         loadInbox();
         loadContacts();
      }
  }, [token, activeTab]); // Load lại khi đổi tab để nhạy bén

  // ---------- 3. SEARCH ENGINE MỚI CÓ STATUS ----------
  useEffect(() => {
    if (!token) return;
    const delayDebounceFn = setTimeout(async () => {
      if(searchQuery.length > 0) {
          try {
            const res = await axios.get(`${API_BASE_URL}/api/users/search?q=${searchQuery}`, {
              headers: { Authorization: `Bearer ${token}` }
            });
            setSearchResults(res.data);
          } catch (e) { console.log('Lỗi search', e); }
      } else { setSearchResults([]); }
    }, 500); 

    return () => clearTimeout(delayDebounceFn);
  }, [searchQuery, token]);

  // ---------- 4. HÀNH ĐỘNG MẠNG XÃ HỘI (KẾT BẠN/NHÓM) ----------
  const sendFriendRequest = async (id) => {
      try {
          await axios.post(`${API_BASE_URL}/api/friends/request/${id}`, {}, { headers: { Authorization: `Bearer ${token}` }});
          alert("Tuyệt vời! Lời mời đã được xịt đi!");
          setSearchQuery(''); // Xoá ô search
      } catch(e) { alert(e.response?.data || "Lỗi"); }
  }

  const acceptFriendRequest = async (friendshipId) => {
      try {
          await axios.post(`${API_BASE_URL}/api/friends/accept/${friendshipId}`, {}, { headers: { Authorization: `Bearer ${token}` }});
          loadContacts();
      } catch(e) { console.error(e); }
  }

  const createGroup = async () => {
      const gName = window.prompt("Nhập Tên Bang Phái của bạn:");
      if (!gName) return;
      try {
          const res = await axios.post(`${API_BASE_URL}/api/groups/create`, `"${gName}"`, { 
              headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }
          });
          alert(`Chúc mừng! Mã bí mật của phòng là: ${res.data.groupId}. Hãy gửi mã này cho anh em để xin vào nhóm!`);
          loadContacts();
      } catch(e) { console.error(e); }
  }

  const requestJoinGroup = async () => {
      const gId = window.prompt("Nhập Mã Nhóm (Ví dụ HD82JDM):");
      if (!gId) return;
      try {
          const check = await axios.get(`${API_BASE_URL}/api/groups/search/${gId}`, { headers: { Authorization: `Bearer ${token}` }});
          if(check.data) {
              if (check.data.joinStatus === 1) alert("Sếp đã ở trong nhóm này rồi cơ mà!");
              else if (check.data.joinStatus === 0) alert("Đã nộp đơn rồi sếp ơi, đợi Admin duyệt đi!");
              else {
                  if(window.confirm(`Tìm thấy nhóm: ${check.data.name}. Nộp đơn nhập hộ khẩu?`)) {
                      await axios.post(`${API_BASE_URL}/api/groups/join/${gId}`, {}, { headers: { Authorization: `Bearer ${token}` }});
                      alert("Đã gõ cửa. Việc còn lại do số phận!");
                  }
              }
          }
      } catch(e) { alert("Mã nhóm sai bét hoặc ổ nhện không tồn tại!"); }
  }

  const approveGroupRequest = async (groupId, userId) => {
      try {
          await axios.post(`${API_BASE_URL}/api/groups/${groupId}/approve/${userId}`, {}, { headers: { Authorization: `Bearer ${token}` }});
          loadContacts();
          alert("Đã duyệt biên chế thành công!");
      } catch(e) { console.log(e); }
  }

  // ---------- 5. MỞ KHÓA LỊCH SỬ CHAT ----------
  const openChatContext = async (id, name, type, fStatus = 1) => {
    setTargetId(id);
    setTargetName(name);
    setChatType(type);
    setCurrentFriendStatus(fStatus);
    setMessages([]); 
    try {
        const endpoint = type === 'group' ? `/api/chats/groups/${id}` : `/api/chats/${id}`;
        const res = await axios.get(`${API_BASE_URL}${endpoint}`, {
            headers: { Authorization: `Bearer ${token}` }
        });
        const historyMapped = res.data.map(m => ({
            id: m.id, senderId: m.senderId, senderName: m.senderName, senderAvatar: m.senderAvatar,
            content: m.content, sentAt: m.sentAt, isMine: m.senderId === user.id, isRevoked: m.isRevoked, isRead: m.isRead
        }));
        setMessages(historyMapped);
    } catch(e) { console.log(e); }
  };

  // ---------- 6. SIGNALR WEBSOCKET ----------
  useEffect(() => {
    if (token && user) {
      const newConnection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_BASE_URL}/chathub`, { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .build();

      newConnection.on("ReceivePrivateMessage", (id, senderId, messageContent, sentAt, isRevoked, isRead) => {
        setMessages(prev => [...prev, { id, senderId, content: messageContent, sentAt, isMine: false, isRevoked, isRead }]);
        loadInbox();
      });

      newConnection.on("ReceiveGroupMessage", (id, groupName, senderId, senderName, senderAvatar, messageContent, sentAt, isRevoked, isRead) => {
         if (targetIdRef.current === groupName) {
             setMessages(prev => [...prev, { id, senderId, senderName, senderAvatar, content: messageContent, sentAt, isMine: senderId === userRef.current.id, isRevoked, isRead }]);
         }
      });

      newConnection.on("MessageRevoked", (messageId) => {
        setMessages(prev => prev.map(m => m.id === messageId ? { ...m, isRevoked: true } : m));
        loadInbox();
      });

      newConnection.on("MessageRead", (messageId) => {
        setMessages(prev => prev.map(m => m.id === messageId ? { ...m, isRead: true } : m));
      });

      newConnection.on("UserTyping", (userId, isTyping) => {
        setTypingUsers(prev => ({ ...prev, [userId]: isTyping }));
      });

      newConnection.start().catch(err => console.error(err));
      setConnection(newConnection);
      return () => newConnection.stop();
    }
  }, [token, user]);

  useEffect(() => {
    if (messages.length > 0 && targetId && chatType === 'direct') {
      const lastMsg = messages[messages.length - 1];
      if (!lastMsg.isMine && !lastMsg.isRead && !lastMsg.isRevoked && connection) {
        connection.invoke("MarkAsRead", lastMsg.id, lastMsg.senderId).catch(console.error);
        setMessages(prev => prev.map(m => m.id === lastMsg.id ? { ...m, isRead: true } : m));
      }
    }
  }, [messages, connection, targetId, chatType]);

  const revokeMessage = async (msgId) => {
      if (window.confirm("Huỷ diệt tin nhắn này khỏi vũ trụ?")) {
          try { await connection.invoke("RevokeMessage", msgId); } catch(e) { console.error(e); }
      }
  };

  const sendMessage = async () => {
    if (!targetId || !inputText || !connection) return;
    try {
      const invokeMethod = chatType === 'group' ? "SendMessageToGroup" : "SendMessageToUser";
      const sentMsg = await connection.invoke(invokeMethod, targetId, inputText);
      
      if (chatType === 'direct') {
          setMessages(prev => [...prev, { 
              id: sentMsg.id, senderId: user.id, content: inputText, sentAt: sentMsg.sentAt, 
              isMine: true, isRevoked: false, isRead: false
          }]);
      }

      setInputText('');
      connection.invoke("TypingToggled", targetId, false);
      if (chatType === 'direct') loadInbox(); 
    } catch (e) { console.error(e); }
  };

  // =========== RENDER CHÀO ===========
  if (!user) {
    return (
      <div className="login-container">
        <div className="glass-card">
          <h1>Zalo Mini</h1>
          <p>Mạng xã hội Real-time Siêu Cấp</p>
          <div className="google-btn-wrapper">
            <GoogleLogin onSuccess={handleLoginSuccess} onError={() => console.log('Chạm nút Failed!')} />
          </div>
        </div>
      </div>
    );
  }

  // =========== RENDER CHÍNH ===========
  return (
    <div className="chat-container">
      {/* 🚀 CỘT TRÁI - QUẢN TRỊ VIÊN */}
      <div className="sidebar glass-panel">
        <div className="my-profile">
          <img src={user.avatar} alt="avatar" />
          <div className="info">
            <h2>{user.name}</h2>
            <p className="online-status">🟢 Đang hoạt động</p>
          </div>
          <button style={{marginLeft: 'auto', background: '#ffebee', color: 'red', border: 'none', padding: '5px 10px', borderRadius: '5px', cursor: 'pointer', fontSize: '11px', fontWeight: 'bold'}} onClick={handleLogout}>Trốn Ép Ký</button>
        </div>
        
        {/* TABS CHUYỂN MẠCH */}
        <div className="sidebar-tabs">
            <div className={`tab-btn ${activeTab === 'messages' ? 'active' : ''}`} onClick={() => setActiveTab('messages')}>Tin nhắn</div>
            <div className={`tab-btn ${activeTab === 'contacts' ? 'active' : ''}`} onClick={() => setActiveTab('contacts')}>Danh bạ</div>
        </div>

        {activeTab === 'messages' && (
            <>
                <div className="search-box">
                    <input type="text" placeholder="🔍 Tìm bằng Tên/ Email..." value={searchQuery} onChange={(e) => setSearchQuery(e.target.value)} />
                </div>
                <div className="inbox-list">
                {searchQuery.length > 0 ? (
                    searchResults.map(p => (
                    <div key={p.id} className="inbox-item" onClick={() => openChatContext(p.id, p.name, 'direct', p.friendStatus)}>
                        <img src={p.avatar} alt="ava" />
                        <div className="inbox-info">
                            <div className="inbox-name">{p.name}</div>
                            {p.friendStatus === 1 ? <div style={{color:'green', fontSize:'11px'}}>Đã là bạn bè</div> : 
                             p.friendStatus === 0 ? <div style={{color:'orange', fontSize:'11px'}}>Đang chờ duyệt</div> : 
                             <div style={{color:'gray', fontSize:'11px'}}>Chưa kết bạn</div>}
                        </div>
                        {p.friendStatus === -1 && <button className="add-friend-btn" onClick={(e) => { e.stopPropagation(); sendFriendRequest(p.id); }}>Kết Bạn</button>}
                    </div>
                    ))
                ) : (
                    inbox.map(ib => (
                    <div key={ib.targetId} className="inbox-item" onClick={() => openChatContext(ib.targetId, ib.name, 'direct', 1)}>
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
            </>
        )}

        {activeTab === 'contacts' && (
            <div className="contacts-list" style={{overflowY: 'auto', flex: 1, padding: '10px'}}>
                <div style={{display:'flex', gap:'5px', marginBottom:'15px'}}>
                    <button className="action-btn" onClick={createGroup} style={{flex:1, background: '#e3f2fd', color: '#0d47a1', border:'none', padding:'10px', borderRadius:'10px', cursor:'pointer', fontWeight:'bold'}}>🏕️ Lập Bang</button>
                    <button className="action-btn" onClick={requestJoinGroup} style={{flex:1, background: '#f3e5f5', color: '#4a148c', border:'none', padding:'10px', borderRadius:'10px', cursor:'pointer', fontWeight:'bold'}}>🕵️ Xin Lót Ổ</button>
                </div>

                {pendingRequests.length > 0 && (
                    <>
                    <div className="section-title" style={{fontSize:'12px', fontWeight:'bold', color:'#777', marginBottom:'10px'}}>ĐƠN XIN KẾT BẠN ({pendingRequests.length})</div>
                    {pendingRequests.map(r => (
                        <div key={r.friendshipId} className="contact-item" style={{display:'flex', alignItems:'center', marginBottom:'10px'}}>
                            <img src={r.senderAvatar} alt="ava" style={{width:'35px', borderRadius:'50%', marginRight:'10px'}} />
                            <div style={{flex: 1, fontSize:'13px', fontWeight:'bold'}}>{r.senderName}</div>
                            <button onClick={() => acceptFriendRequest(r.friendshipId)} style={{background:'#4caf50', color:'white', border:'none', padding:'5px 10px', borderRadius:'5px', cursor:'pointer', fontSize:'11px'}}>Duyệt</button>
                        </div>
                    ))}
                    </>
                )}

                {pendingGroupRequests.length > 0 && (
                    <>
                    <div className="section-title" style={{fontSize:'12px', fontWeight:'bold', color:'#777', marginBottom:'10px'}}>DUYỆT VÀO NHÓM ({pendingGroupRequests.length})</div>
                    {pendingGroupRequests.map(pgr => (
                        <div key={`${pgr.groupId}-${pgr.id}`} className="contact-item" style={{display:'flex', alignItems:'center', marginBottom:'10px', padding:'10px', border:'1px dashed #ccc'}}>
                            <img src={pgr.avatarUrl} alt="ava" style={{width:'30px', borderRadius:'50%', marginRight:'10px'}} />
                            <div style={{flex: 1, fontSize:'12px'}}><b>{pgr.displayName}</b> xin vào nhóm <b>{pgr.groupName}</b></div>
                            <button onClick={() => approveGroupRequest(pgr.groupId, pgr.id)} style={{background:'#ff9800', color:'white', border:'none', padding:'5px 10px', borderRadius:'5px', cursor:'pointer', fontSize:'11px'}}>Thu Nhận</button>
                        </div>
                    ))}
                    </>
                )}

                <div className="section-title" style={{fontSize:'12px', fontWeight:'bold', color:'#777', margin:'15px 0 10px 0'}}>NHÓM CỦA TÔI ({myGroups.length})</div>
                {myGroups.map(g => (
                    <div key={g.id} className="contact-item" onClick={() => openChatContext(g.id, g.name, 'group')} style={{display:'flex', alignItems:'center', marginBottom:'10px', cursor:'pointer'}}>
                        <img src={g.avatarUrl} alt="ava" style={{width:'35px', borderRadius:'50%', marginRight:'10px'}} />
                        <div style={{flex: 1, fontSize:'14px', fontWeight:'bold'}}>{g.name} {g.isAdmin && '👑'}</div>
                    </div>
                ))}

                <div className="section-title" style={{fontSize:'12px', fontWeight:'bold', color:'#777', margin:'15px 0 10px 0'}}>BẠN BÈ ({friends.length})</div>
                {friends.map(f => (
                    <div key={f.id} className="contact-item" onClick={() => openChatContext(f.id, f.name, 'direct')} style={{display:'flex', alignItems:'center', marginBottom:'10px', cursor:'pointer'}}>
                        <img src={f.avatar} alt="ava" style={{width:'35px', borderRadius:'50%', marginRight:'10px'}} />
                        <div style={{flex: 1, fontSize:'14px', fontWeight:'bold'}}>{f.name}</div>
                    </div>
                ))}
            </div>
        )}
      </div>
      
      {/* 🚀 CỘT PHẢI - CHIẾN TRƯỜNG */}
      <div className="chat-area glass-panel">
        <div className="chat-header">
          {targetName ? `${chatType === 'group' ? '👨‍👩‍👧‍👦' : '👤'} ${targetName}` : '👈 Khám phá Trạm Chuyển Phát!'}
        </div>
        
        <div className="messages-list">
          {/* CẢNH BÁO AN TOÀN NẾU GIAO DỊCH NGƯỜI LẠ */}
          {chatType === 'direct' && currentFriendStatus === -1 && targetId && (
              <div className="system-alert" style={{background: '#fff3cd', color: '#856404', padding: '10px', borderRadius: '8px', margin: '15px', textAlign: 'center', fontSize: '13px', fontWeight: 'bold'}}>
                  ⚠️ CẢNH BÁO: Bạn và người này CHƯA KẾT BẠN. Hãy cẩn thận khi chuyển tiền!
              </div>
          )}

          {messages.map((msg, index) => (
            <div key={msg.id || index} className={`msg-row ${msg.isMine ? 'mine' : 'theirs'} ${msg.senderId === 'Hệ Thống' ? 'system-row' : ''}`}>
              
              {chatType === 'group' && !msg.isMine && msg.senderId !== 'Hệ Thống' && (
                <div className="group-avt">
                   <img src={msg.senderAvatar || 'https://cdn-icons-png.flaticon.com/512/148/148767.png'} alt="avt" />
                </div>
              )}

              <div className="msg-bubble-container">
                  {chatType === 'group' && !msg.isMine && msg.senderId !== 'Hệ Thống' && (
                      <div className="group-name">{msg.senderName}</div>
                  )}

                  <div className={`msg-bubble ${msg.isMine ? 'mine' : 'theirs'} ${msg.isRevoked ? 'revoked' : ''} ${msg.senderId === 'Hệ Thống' ? 'system' : ''}`}>
                    {msg.isMine && !msg.isRevoked && msg.senderId !== 'Hệ Thống' && (
                        <button className="revoke-btn" onClick={() => revokeMessage(msg.id)}>Thu</button>
                    )}

                    <div className="msg-content">
                        {msg.isRevoked ? "⚠️ Tin nhắn đã bị rụt lại" : msg.content}
                    </div>
                    
                    {msg.senderId !== 'Hệ Thống' && (
                        <div className="msg-time">{new Date(msg.sentAt).toLocaleTimeString()}</div>
                    )}
                    
                    {msg.isMine && msg.isRead && index === messages.length - 1 && chatType === 'direct' && (
                        <img src="https://cdn-icons-png.flaticon.com/512/148/148767.png" className="read-receipt" alt="seen" title="Đã xem" />
                    )}
                  </div>
              </div>
            </div>
          ))}
          
          {typingUsers[targetId] && (
             <div className="typing-indicator">Người ấy đang soạn phím . . .</div>
          )}
        </div>
        
        <div className="chat-input-area" style={{opacity: targetId ? 1 : 0.5, pointerEvents: targetId ? 'auto' : 'none'}}>
          <input 
            type="text" 
            placeholder="Luồn tin nhắn vào đây..." 
            value={inputText}
            onChange={(e) => {
                setInputText(e.target.value);
                if (connection && targetId) connection.invoke("TypingToggled", targetId, e.target.value.length > 0);
            }}
            onKeyDown={(e) => e.key === 'Enter' && sendMessage()}
          />
          <button onClick={sendMessage}>Bắn</button>
        </div>
      </div>
    </div>
  );
}

export default App;
