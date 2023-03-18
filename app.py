import logging
from telegram import Update
from telegram.ext import Updater, CommandHandler, MessageHandler, Filters, CallbackContext
import requests
import os
from flask import Flask, render_template, request, redirect, url_for, send_from_directory
app = Flask(__name__)

logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
logger = logging.getLogger()

OPENAI_API_KEY = os.environ.get("OPEN_API_KEY")
TELEGRAM_BOT_TOKEN = os.environ.get("TELEGRAM_BOT_TOKEN")
CHATGPT_API_ENDPOINT = os.environ.get("CHATGPT_API_ENDPOINT")

def chat_gpt_response(prompt):
    headers = {
        'Content-Type': 'application/json',
        'Authorization': f'Bearer {OPENAI_API_KEY}'
    }
    data = {
        'prompt': prompt,
        'max_tokens': 100,
        'temperature': 0.8,
    }
    response = requests.post(CHATGPT_API_ENDPOINT, headers=headers, json=data)
    response_json = response.json()

    if 'choices' in response_json and len(response_json['choices']) > 0:
        return response_json['choices'][0]['text'].strip()
    else:
        return "Error: Unable to get a response from the ChatGPT API."
def handle_text_message(update: Update, context: CallbackContext):
    user = update.message.from_user
    chat_id = update.message.chat.id
    user_message = update.message.text

    logger.info(f"Received message in chat {chat_id}: {user_message}")

    chat_gpt_prompt = f"{user_message}"
    chat_gpt_reply = chat_gpt_response(chat_gpt_prompt)
    update.message.reply_text(chat_gpt_reply)

@app.route('/index')
def index():
   print('Request for index page received')
   return render_template('index.html')

@app.route('/hello', methods=['POST'])
def hello():
   name = request.form.get('name')

   if name:
       print('Request for hello page received with name=%s' % name)
       return render_template('hello.html', name = name)
   else:
       print('Request for hello page received with no name or blank name -- redirecting')

@app.route(f'/{TELEGRAM_BOT_TOKEN}', methods=['POST'])
def webhook_handler():
    update = Update.de_json(request.get_json(force=True), context.bot)
    context.update_queue.put(update)
    return 'OK'

def main():
    logger.info("starting...")
    updater = Updater(TELEGRAM_BOT_TOKEN, use_context=True)
    dp = updater.dispatcher

    logger.info("adding handler...")
    dp.add_handler(MessageHandler(Filters.text & ~Filters.command, handle_text_message))

    # Set webhook
    bot = updater.bot
    logger.info("setting webhook...")
    bot.set_webhook(url=f'https://app-chatgpt-ungerfall.azurewebsites.net/{TELEGRAM_BOT_TOKEN}')

    logger.info("running...")
    app.run()

if __name__ == '__main__':
    main()

