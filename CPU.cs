using System;
using System.Collections.Generic;

namespace c_ip8
{
    public class CPU
    {
        private const int DISPLAY_WIDTH = 64;
        private const int DISPLAY_HEIGHT = 32;

        public byte[] RAM = new byte[4096];
        public byte[] V = new byte[16]; //registers
        public ushort ProgramCounter;
        public ushort I;
        public Stack<ushort> Stack = new Stack<ushort>();
        public byte DelayTimer;
        public byte SoundTimer;
        public byte Input;
        public byte[,] Display = new byte[DISPLAY_WIDTH, DISPLAY_HEIGHT];

        public void LoadProgram(ushort[] program) {
            RAM = new byte[4096];
            
            for(int i = 0; i < program.Length; i++) {
                //according to documentation, chip 8 start at memory location 512
                //the positions before 512 were reserved to the original interpreter

                // position is multiplied by two since each instruction is two bytes
                RAM[512 + i * 2]  = (byte) ((program[i] & 0xFF00) >> 8);
                RAM[513 + i * 2] = (byte) (program[i] & 0x00FF);
            }

            ProgramCounter = 512;
        }        

        private Random rndGenerator = new Random(Environment.TickCount);
        public void Step()
        {
            var opCode = (ushort) (RAM[ProgramCounter] << 8 | RAM[ProgramCounter +1]);
            switch (opCode & 0xF000) //top 4 bites
            {
                case 0x0000:
                    if (opCode == 0x00E0)
                    { //clear screen
                        for (int i = 0; i < DISPLAY_WIDTH; i++)
                        {
                            for (int j = 0; j < DISPLAY_HEIGHT; j++)
                            {
                                Display[i, j] = 0;
                            }
                        }
                    }
                    else if (opCode == 0x00EE)
                    {
                        ProgramCounter = Stack.Pop();
                    }
                    break;
                case 0x1000:
                    ProgramCounter = (ushort)(opCode & 0x0FFF);
                    break;
                case 0x2000:
                    Stack.Push(ProgramCounter);
                    ProgramCounter = (ushort)(opCode & 0x0FFF);
                    break;
                case 0x3000:
                    if (V[opCode & 0x0F00 >> 8] == (opCode & 0x00FF))
                        ProgramCounter += 2;
                    break;
                case 0x4000:
                    if (V[opCode & 0x0F00 >> 8] != (opCode & 0x00FF))
                        ProgramCounter += 2;
                    break;
                case 0x5000:
                    if (V[opCode & 0x0F00 >> 8] == V[opCode & 0x00F0 >> 4])
                        ProgramCounter += 2;
                    break;
                case 0x6000:
                    V[opCode & 0x0F00 >> 8] = (byte)(opCode & 0x00FF);
                    break;
                case 0x7000:
                    V[opCode & 0x0F00 >> 8] += (byte)(opCode & 0x00FF);
                    break;
                case 0x8000:
                    var Vx = (byte)(opCode & 0x0F00 >> 8);
                    var Vy = (byte)(opCode & 0x00F0 >> 4);
                    switch (opCode & 0x000F) //lowest 4 bites
                    {
                        case 0x0000:
                            V[Vx] = V[Vy];
                            break;
                        case 0x0001:
                            V[Vx] = (byte)(V[Vx] & V[Vy]);
                            break;
                        case 0x0002:
                            V[Vx] = (byte)(V[Vx] | V[Vy]);
                            break;
                        case 0x0003:
                            V[Vx] = (byte)(V[Vx] ^ V[Vy]);
                            break;
                        case 0x0004:
                            var sum = (byte)(V[Vx] + V[Vy]);
                            V[Vx] = (byte)(sum & 0x00FF);
                            V[15] = (byte)(sum > 255 ? 1 : 0); //carry flag
                            break;
                        case 0x0005:
                            V[15] = (byte)(V[Vx] > V[Vy] ? 1 : 0);
                            V[Vx] = (byte)((V[Vx] - V[Vy]) & 0x00FF);
                            break;
                        case 0x0006:
                            V[15] = (byte)((V[Vx] & 0x0001) == 1 ? 1 : 0);
                            V[Vx] = (byte)(V[Vx] >> 1);
                            break;
                        case 0x0007:
                            V[15] = (byte)(V[Vy] > V[Vx] ? 1 : 0);
                            V[Vx] = (byte)((V[Vy] - V[Vx]) & 0x00FF);
                            break;
                        case 0x000E:
                            V[15] = (byte)((V[Vx] & 0x1000) == 1 ? 1 : 0);
                            V[Vx] = (byte)(V[Vx] << 1);
                            break;
                        default:
                            Console.WriteLine($"unknown opcode {opCode.ToString("X4")}");
                            break;
                    }
                    break;
                case 0x9000:
                    if (V[opCode & 0x0F00 >> 8] != V[opCode & 0x00F0 >> 4])
                        ProgramCounter += 2;
                    break;
                case 0xA000:
                    I = (ushort)(opCode & 0x0FFF);
                    break;
                case 0xB000:
                    ProgramCounter = (ushort)(V[0] + (opCode & 0x0FFF));
                    break;
                case 0xC000:
                    var rnd = rndGenerator.Next(256);
                    V[opCode & 0x0F00] = (byte)(rnd & (opCode & 0x00FF));
                    break;
                case 0xD000: //TODO: this is probably wrong
                    V[15] = 0;
                    var px = opCode & 0x0F00 >> 8;
                    var py = opCode & 0x00F0 >> 4;
                    var spriteSize = opCode & 0x000F;

                    for (int i = 0; i < spriteSize; i++)
                    {
                        var spriteToLoad = RAM[I + i];

                        for (int j = 0; j < 8; j++)
                        {
                            //7-j -> MSB is leftmost bit
                            //0x01 -> get a single bit value
                            byte currentPixelToBeLoaded = (byte)((spriteToLoad >> (7 - j)) & 0x01);
                            if (currentPixelToBeLoaded == 1 && Display[px + j, py] == 1)
                            {
                                V[15] = 1;
                            }
                            Display[px + j, py] = (byte)(Display[px + j, py] ^ currentPixelToBeLoaded);
                        }
                    }
                    break;
                case 0xE000:
                    var inputOpCode = (byte)(opCode & 0x00FF);
                    var x = (byte)((opCode & 0x0F00) >> 8);
                    if (inputOpCode == 0x009E)
                    {
                        if (Input == V[x])
                        {
                            ProgramCounter += 2;
                        }
                    }
                    else if (inputOpCode == 0x00A1)
                    {
                        if (Input != V[x])
                        {
                            ProgramCounter += 2;
                        }
                    }
                    break;
                case 0xF000:
                    x = (byte)((opCode & 0x0F00) >> 8);
                    var lastNibbles = (byte)(opCode & 0x00FF);
                    switch (lastNibbles)
                    {
                        case 0x07:
                            V[x] = DelayTimer;
                            break;
                        case 0x0A:
                            Console.WriteLine("Waiting for keypress");
                            var k = Console.ReadKey();
                            //handle input
                            V[x] = Input;
                            break;
                        case 0x15:
                            DelayTimer = V[x];
                            break;
                        case 0x18:
                            SoundTimer = V[x];
                            break;
                        case 0x1E:
                            I = (ushort)(I + V[x]);
                            break;
                        case 0x29:
                            throw new Exception("not not handling system font");
                        case 0x33:
                            RAM[I] = (byte)(V[x] / 100);
                            RAM[I + 1] = (byte)(V[x] % 100 / 10);
                            RAM[I + 3] = (byte)(V[x] % 10);
                            break;
                        case 0x55:
                            for (int i = 0; i < x; i++)
                            {
                                RAM[I + i] = V[i];
                            }
                            break;
                        case 0x65:
                            for (int i = 0; i < x; i++)
                            {
                                V[i] = RAM[I + 1];
                            }
                            break;

                        default:
                            throw new Exception("opcode not supported");
                    }

                    break;
                default:
                    throw new Exception("opcode not supported");
            }
        
        ProgramCounter += 2;
        }
    }
}