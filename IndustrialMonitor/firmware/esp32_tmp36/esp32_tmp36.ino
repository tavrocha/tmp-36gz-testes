#include <Arduino.h>

const int pinSensor = 32; // Conecte a saída do TMP36 neste pino (ADC)

void setup() {
  Serial.begin(115200);
  analogReadResolution(12); // Resolução de 12 bits do ESP32 (0-4095)
}

void loop() {
  int valorAnalogico = analogRead(pinSensor);
  
  // Converte a leitura analógica para milivolt (assumindo 3.3V)
  float voltagem = valorAnalogico * (3.3 / 4095.0);
  
  // Converte a voltagem para temperatura (°C) do TMP36
  float temperatura = (voltagem - 0.5) * 100.0;

  // Envia a temperatura no formato que o C# lê
  Serial.print("TEMP=");
  Serial.println(temperatura);

  delay(1000);
}