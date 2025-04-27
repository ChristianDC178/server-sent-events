import React, { useState, useEffect } from 'react';
import './App.css';

function App() {
  const [connected, setConnected] = useState(false);
  const [messages, setMessages] = useState([]);
  const [inputMessage, setInputMessage] = useState('');

  useEffect(() => {
    // Connect to SSE endpoint
    const eventSource = new EventSource('http://localhost:5059/sse');
    
    eventSource.onopen = () => {
      setConnected(true);
      console.log('SSE connection opened');
    };
    
    eventSource.onmessage = (event) => {
      setMessages((prevMessages) => [...prevMessages, event.data]);
    };
    
    eventSource.onerror = (error) => {
      console.error('SSE error:', error);
      setConnected(false);
      eventSource.close();
    };
    
    // Clean up on unmount
    return () => {
      eventSource.close();
    };
  }, []);

  const sendMessage = async () => {
    if (inputMessage.trim() === '') return;
    
    try {
      await fetch('http://localhost:5059/broadcast', {
        method: 'POST',
        body: inputMessage
      });
      setInputMessage('');
    } catch (error) {
      console.error('Error sending message:', error);
    }
  };

  return (
    <div className="App">
      <header className="App-header">
        <h1>Server-Sent Events Demo</h1>
        <div className="connection-status">
          Status: {connected ? 'Connected' : 'Disconnected'}
        </div>
        
        <div className="message-container">
          <h2>Messages</h2>
          <div className="messages">
            {messages.map((message, index) => (
              <div key={index} className="message">{message}</div>
            ))}
          </div>
        </div>
        
        <div className="input-container">
          <input
            type="text"
            value={inputMessage}
            onChange={(e) => setInputMessage(e.target.value)}
            placeholder="Type a message..."
          />
          <button onClick={sendMessage}>Broadcast</button>
        </div>
      </header>
    </div>
  );
}

export default App;