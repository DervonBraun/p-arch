// server.js
const express = require('express');
const app = express();
app.use(express.json());

app.post('/scan', (req, res) => {
    const { objectId, messages } = req.body;
    const lastMsg = messages[messages.length - 1]?.content || '';
    res.json({
        response: `[Тест] Объект: ${objectId}. Вопрос получен: "${lastMsg}". Groq API не подключён.`,
        blueTokensAwarded: 1
    });
});

app.listen(3000, () => console.log('Server on :3000'));