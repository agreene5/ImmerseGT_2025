from fastapi import FastAPI, File, UploadFile
from fastapi.responses import JSONResponse, FileResponse
from ultralytics import YOLO
from gtts import gTTS
import cv2
import numpy as np
import os
import uuid
from fastapi import Form
import io
from PIL import Image

app = FastAPI()


# Load YOLOv8s model
model = YOLO("yolov8s.pt")
model.overrides['conf'] = 0.25
model.overrides['iou'] = 0.45
model.overrides['agnostic_nms'] = False
model.overrides['max_det'] = 1000


@app.post("/upload")
async def upload_image(file: UploadFile = File(...)):
    contents = await file.read()


    nparr = np.frombuffer(contents, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)


    results = model.predict(source=img)
    boxes = results[0].boxes


    output = []
    for box in boxes:
        cls_id = int(box.cls[0])
        label = model.names[cls_id]
        conf = float(box.conf[0])
        coords = box.xyxy[0].tolist()


        output.append({
            "label": label,
            "confidence": conf,
            "bbox": coords
        })


    return JSONResponse(content={"detections": output})


@app.post("/tts")
async def generate_audio(text: str = Form(...)):
    tts = gTTS(text)
    filename = f"tts_{uuid.uuid4().hex}.mp3"
    path = os.path.join("tts_audio", filename)



    os.makedirs("tts_audio", exist_ok=True)
    tts.save(path)


    return FileResponse(path, media_type="audio/mpeg", filename=filename)

@app.post("/process-frame")
async def process_frame(file: UploadFile = File(...)):
    contents = await file.read()
    image = Image.open(io.BytesIO(contents)).convert("RGB")

    results = model.predict(image, conf=0.3)[0]
    filtered_objects = []

    img_width, img_height = image.size

    for box in results.boxes:
        x1, y1, x2, y2 = box.xyxy[0]
        cls = int(box.cls[0])
        label = model.model.names[cls]

        w = x2 - x1
        h = y2 - y1
        area = w * h

        center_x = (x1 + x2) / 2

        if area > (0.1 * img_width * img_height) and (0.3 * img_width < center_x < 0.7 * img_width):
            filtered_objects.append(label)

    if not filtered_objects:
        description = "No important objects detected nearby."
    else:
        description = "You are near a " + ", a ".join(filtered_objects)

    print("[Narrating]:", description)
    audio_file = generate_audio(description)
    return FileResponse(audio_file, media_type="audio/mpeg")

def generate_audio(text):
    from gtts import gTTS
    filename = f"speech_{uuid.uuid4()}.mp3"
    tts = gTTS(text)
    tts.save(filename)
    return filename

