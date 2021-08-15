using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using Memory.Win64;
using System.IO;

namespace TekkenTrainer
{
    public partial class Form1 : Form
    {
        class Node
        {
            public string name;
            public ulong[] ptr;
            public Node()
            {
                Initialize(string.Empty);

            }
            public Node(string n, ulong[] o = null)
            {
                Initialize(n, o);
            }
            public void Initialize(string n, ulong[] o = null)
            {
                name = n; ptr = o;
            }
        }

        class Reqs
        {
            public string name;
            public int[,] ptr;
            public Reqs()
            {
                Initialize(string.Empty);
            }
            public Reqs(string n, int[,] r = null)
            {
                Initialize(n, r);
            }
            public void Initialize(string n, int[,] r = null)
            {
                name = n; ptr = r;
            }
        }

        class Node2
        {
            public int index;
            public int value;
            public Node2(int idx = -1, int val = -1)
            {
                index = idx; value = val;
            }
        }

        // Moveset Structure Offsets
        enum OFFSETS
        {
            reaction_list = 0x150,
            requirements = 0x160,
            hit_condition = 0x170,
            projectile = 0x180,
            pushback = 0x190,
            pushback_extra = 0x1A0,
            cancel_list = 0x1B0,
            group_cancel_list = 0x1C0,
            cancel_extra = 0x1D0,
            extraprops = 0x1E0,
            moves = 0x210,
            voice_clip = 0x220
        };

        // Move Attribute Offsets
        enum Offsets
        {
            name = 0x0,
            anim_name = 0x8,
            anim_addr = 0x10,
            vuln = 0x18,
            hitlevel = 0x1c,
            cancel_addr = 0x20,
            transition = 0x54,
            anim_len = 0x68,
            startup = 0xA0,
            recovery = 0xA4,
            hitbox = 0x9C,
            hit_cond_addr = 0x60,
            ext_prop_addr = 0x80,
            voiceclip_addr = 0x78
        };

        MemoryHelper64 mem = null;
        ulong baseAddress = 0;
        // GAME VERSION: v4.20

        // Structure Addresses
        public static ulong p1struct;
        public static ulong p1profileStruct;
        public static ulong p2profileStruct;
        public static ulong visuals;

        // Costume Related stuff
        const string cs_kaz_final = "/Game/Demo/StoryMode/Character/Sets/CS_KAZ_final.CS_KAZ_final";
        const string cs_hei_final = "/Game/Demo/StoryMode/Character/Sets/CS_HEI_final.CS_HEI_final";
        const string cs_mrx_final = "/Game/Demo/StoryMode/Character/Sets/CS_MRX_final.CS_MRX_final";

        readonly List<Node> fileData = new List<Node>();
        readonly List<Reqs> requirements = new List<Reqs>();
        bool IsRunning = false; // Variable to check if the game is running or not
        public Form1()
        {
            InitializeComponent();
            Panels_Visibility(false);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// MAIN FORM
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void Form1_Load(object sender, EventArgs e)
        {
            MainLoop_ThreadCaller();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// THREAD INVOKES TO ACCESS UI ELEMENTS
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public void AppendTextBox(string value)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(AppendTextBox), new object[] { value });
                return;
            }
            textBox1.Text += value;
        }

        public void ClearTextBox(string value)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(ClearTextBox), new object[] {value});
                return;
            }
            textBox1.Clear();
        }

        private bool Checkbox_Get(int i)
        {
            // result value.
            bool result = false;

            CheckBox[] boxes = {
                checkBox1, checkBox2, checkBox3, checkBox4, checkBox5, checkBoxCostumes
            };
            if (i < 1 || i > 6) return false;
            i--;
            // define a function which assigns the checkbox checked state to the result
            Action checkCheckBox = new Action(() => result = boxes[i].Checked);
            // check if it should be invoked.      
            if (boxes[i].InvokeRequired)
                boxes[i].Invoke(checkCheckBox);
            else
                checkCheckBox();

            // return the result.
            return result;
        }

        private void Checkbox_Set(int i, bool value)
        {
            CheckBox[] boxes = {
                checkBox1, checkBox2, checkBox3, checkBox4, checkBox5, checkBoxCostumes
            };
            if (i < 1 || i > 6) return;
            i--;
            // define a function which assigns the value to the checkbox
            Action checkCheckBox = new Action(() => boxes[i].Checked = value);
            // check if it should be invoked.      
            if (boxes[i].InvokeRequired)
                boxes[i].Invoke(checkCheckBox);
            else
                checkCheckBox();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// BUTTONS
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // This is the button for Kazuya
        private void Button_kazuya_Click(object sender, EventArgs e)
        {
            panel_kazuya.Visible = true;
            button_back.Visible = true;
        }

        // This is the button for Heihachi
        private void Button_heihachi_Click(object sender, EventArgs e)
        {
            panel_heihachi.Visible = true;
            button_back.Visible = true;
        }

        // This is the button for Akuma
        private void Button_akuma_Click(object sender, EventArgs e)
        {
            panel_akuma.Visible = true;
            button_back.Visible = true;
        }

        // This button is for Devil Kazumi
        private void Button_kazumi_Click(object sender, EventArgs e)
        {
            panel_kazumi.Visible = true;
            button_back.Visible = true;
        }

        // This button is for Asura Jin
        private void Button_Jin_Click(object sender, EventArgs e)
        {
            panel_jin.Visible = true;
            button_back.Visible = true;
        }

        // For bringing Forward instructions 
        private void button3_Click(object sender, EventArgs e)
        {
            panel_instructions.BringToFront();
            panel_instructions.Visible = true;
            button_back.Visible = true;
        }

        // For going back to main menu
        private void button_back_Click(object sender, EventArgs e)
        {
            Panels_Visibility(false);
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// MAIN THREADS
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void MainLoop_ThreadCaller()
        {
            Thread BOSSES = new Thread(MainLoop) { IsBackground = true };
            BOSSES.Start();
        }
        private void GameRunning()
        {
            Thread BOSSES = new Thread(GameRunningLoop) { IsBackground = true };
            BOSSES.Start();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// THREADS FUNCTIONS
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void MainLoop()
        {
            ReadAddressesFromFile();
            if (fileData.Count == 0)
            {
                MessageBox.Show("Unable to Read Data from the following file: addresses.txt\nClosing Program.");
                CloseProgram();
                return;
            }
            int c1 = 0, c2, c3;
            GameRunning(); // Enabling the thread that will keep track of the running game
            mem = null;
            Process process = null;
            while (true)
            {
                Thread.Sleep(100);
                if (process != null) continue; // If Process already found and attached to, then keep running.
                c2 = 0;
                c3 = 0;
                process = null;
                try { process = Process.GetProcessesByName("TekkenGame-Win64-Shipping")[0]; }
                catch (Exception ex)
                {
                    if ((uint)ex.HResult == 0x80131508) { }
                    else throw ex;
                }
                if (process != null)    // Process Found
                {
                    if (process.Id < 0) // An Error Occured Attaching to Process
                    {
                        if (c1 == 0) AppendTextBox("\r\nFailed to attach to process.");
                        c1 += 1; Thread.Sleep(1000);
                    }
                    else if (process.Id == 0) // Game not found running
                    {
                        if (c1 == 0) {
                            ClearTextBox("");
                            AppendTextBox("TEKKEN 7 Not Running. Please Run the Game");
                        }
                        c1 += 1; Thread.Sleep(1000);
                        process = null;
                        continue;
                    }
                    // Process Successfully Found
                    if (mem == null) mem = new MemoryHelper64(process);
                    else mem.SetProcess(process);
                    // Finding Addresses
                    baseAddress = mem.GetBaseAddress();
                    ClearTextBox("");
                    AppendTextBox("Attached to the game\r\nFinding Visuals Address...");
                    visuals = mem.OffsetCalculator(FindInList("visuals"));
                    if (visuals == 0)
                    {
                        process = null; mem.SetProcess(null);
                        continue; // Loop Back if unable to read the address
                    }
                    AppendTextBox("Found!\r\nFinding Player 1 Profile Address...");
                    // Finding P1 Profile Structure Address
                    ulong[] list = FindInList("p1profile");
                    if (list == null) // In case of an error
                    {
                        MessageBox.Show("An Error occured Reading P1 Profile Address from \"addresses.txt\". \nClosing Program.");
                        CloseProgram();
                        return;
                    }
                    while (true) // Loop to find the said address
                    {
                        Thread.Sleep(1000);
                        p1profileStruct = mem.OffsetCalculator(list);
                        if (p1profileStruct == 0)   // Address Not Found
                        {
                            if (!process.HasExited) continue;   // Game Running
                            else
                            {
                                if (c2 == 0)   // Game Not Running
                                {
                                    ClearTextBox("");
                                    AppendTextBox("TEKKEN 7 Not Running. Please Run the Game\r\n");
                                }
                                c2++; Thread.Sleep(1000);
                                break;  // Breaking the loop so program can loop back to attach to game
                            }
                        }
                        else break;
                    }
                    if (c2 >= 1) // If Game is not running then simply loop back
                    {
                        process = null; mem.SetProcess(null);
                        continue;
                    }
                    AppendTextBox("Found!\r\nFinding Player 2 Profile Address...");

                    // Finding P2 Profile Structure Address
                    c3 = 0;
                    list[2] = 0x8;
                    while (true)
                    {
                        Thread.Sleep(1000);
                        p2profileStruct = mem.OffsetCalculator(list);
                        if (p2profileStruct == 0)   // Address Not Found
                        {
                            if (!process.HasExited) // Game Running | We loop until we find the address
                            {
                                // Do Nothing. Program will automatically loop back here
                            }
                            else if (c3 == 0)   // Game Not Running
                            {
                                ClearTextBox("");
                                AppendTextBox("Could not find TEKKEN 7. Please Run the Game\r\n");
                                c3++; Thread.Sleep(1000);
                                break;  // Breaking the loop so program can loop back to attach to game
                            }
                        }
                        else break; // Address Successfully Found
                    }
                    if (c3 >= 1) continue;  // If Address not found then Loop back
                    AppendTextBox("Found!\r\nStarting Script\r\n");
                    BossThreadLoop();
                    mem.SetProcess(null);
                    process = null;
                }
                else
                {
                    ClearTextBox("");
                    AppendTextBox("Could not find TEKKEN 7. Please Run the Game\r\n");
                    //MessageBox.Show("No Process Found After Searching\nApplication Quitting.");
                    //CloseProgram(); return;
                }
            }
        }
        private void GameRunningLoop()
        {
            while (true)
            {
                if (mem == null) IsRunning = false;
                else IsRunning = mem.IsRunning();
                Thread.Sleep(100);
            }
        }
        private void Costumes(int side) // 0 = Left, 1 = Right
        {
            if (!IsRunning) return;
            if (!Checkbox_Get(6)) return;
            int charID = GetCharID(side);
            if (charID == 08 && GetCostumeID(side) == 14) // Preset 8
            {
                Costume(side, 4, cs_hei_final);
            }
            else if (charID == 09 && GetCostumeID(side) == 14) // Preset 8
            {
                Costume(side, 4, cs_kaz_final);
            }
            else if (charID == 32 && GetCostumeID(side) == 11) // Preset 5
            {
                Costume(side, 4, cs_mrx_final);
            }
            else if (charID == 26 && (GetCostumeID(side) == 13 || GetCostumeID(side) == 0)) // Preset 0 or 7
            {
                Thread.Sleep(200);
                LoadCharacter(side, 27);  // Load Devil Kazumi
                Costume(side, 0, "\0");
            }
        }
        private void BossThreadLoop()
        {
            if (!IsRunning) return;
            int side;
            int gameMode;
            bool IsWritten1 = false;
            bool IsWritten2 = false;
            ulong MOVESET1;
            ulong MOVESET2;
            while (IsRunning)
            {
                Thread.Sleep(10);
                gameMode = GameMode();
                if (gameMode == 3 || gameMode == 4 || gameMode == 15) continue;
                else if (gameMode == 6)    // If in versus mode
                {
                    Costumes(0);
                    Costumes(1);
                    MOVESET1 = GetMovesetAddress(0);
                    MOVESET2 = GetMovesetAddress(1);
                    if (!MovesetExists(MOVESET1))
                    {
                        Checkboxes_checks(false); // Disabling All Check Boxes
                        IsWritten1 = false;
                        IsWritten2 = false;
                        continue;
                    }
                    if (!IsWritten1) IsWritten1 = LoadBoss(MOVESET1, 0);
                    if (!IsWritten2) IsWritten2 = LoadBoss(MOVESET2, 1);
                }
                else // In other game modes
                {
                    side = CheckPlayerSide();
                    Costumes(side);
                    MOVESET1 = GetMovesetAddress(side);
                    if (!MovesetExists(MOVESET1))
                    {
                        Checkboxes_checks(false); // Disabling All Check Boxes
                        IsWritten1 = false;
                        continue;
                    }
                    if (!IsWritten1) IsWritten1 = LoadBoss(MOVESET1, side);
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// REMAINING FUNCTIONS
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private bool LoadBoss(ulong MOVESET, int side)
        {
            if (!IsRunning) return false;
            bool Result = false;
            bool Load = false;
            int charID = GetCharID(MOVESET);
            if (charID == 09)
            {
                if (Checkbox_Get(6) && GetCostumeID(side) == 4) Load = true;
                else if (!Checkbox_Get(6)) Load = true;
                else Load = false;
                if (Load)
                {
                    Result = DVKCancelRequirements(MOVESET);
                    Checkbox_Set(1, Result);
                }
            }
            else if (charID == 08)
            {
                if (Checkbox_Get(6) && GetCostumeID(side) == 4) Load = true;
                else if (!Checkbox_Get(6)) Load = true;
                else Load = false;
                if (Load)
                {
                    Result = ASHCancelRequirements(MOVESET);
                    Checkbox_Set(2, Result);
                }
            }
            else if (charID == 32)
            {
                if (Checkbox_Get(6) && GetCostumeID(side) == 4) Load = true;
                else if (!Checkbox_Get(6)) Load = true;
                else Load = false;
                if (Load)
                {
                    Result = SHACancelRequirements(MOVESET);
                    Checkbox_Set(3, Result);
                }
            }
            else if (charID == 27)
            {
                Result = BS7CancelRequirements(MOVESET);
                Checkbox_Set(4, Result);
            }
            else if (charID == 06)
            {
                Result = JINCancelRequirements(MOVESET);
                Checkbox_Set(5, Result);
            }
            else Checkboxes_checks(false); // Disabling All Check Boxes
            return Result;
        }
        
        private ulong GetMovesetAddress(int side)
        {
            bool Error = false;
            ulong p1struct = 0, motbinOffset = 0, p1structsize = 0;
            if (FindInList("p1struct") != null) p1struct = FindInList("p1struct")[0];
            else Error = true;
            if (FindInList("movesetOffset") != null) motbinOffset = FindInList("movesetOffset")[0];
            else Error = true;
            if (FindInList("p1structsize") != null) p1structsize = FindInList("p1structsize")[0];
            else Error = true;
            if (p1struct == 0) Error = true;
            if (motbinOffset == 0) Error = true;
            if (p1structsize == 0) Error = true;
            if (Error)
            {
                AppendTextBox("Error While Reading Moveset Address\r\n");
                return 0;
            }
            return mem.ReadMemory<ulong>(baseAddress + p1struct + motbinOffset + ((ulong)side * p1structsize));
        }
        private void Costume(int side, int value, string costume)
        {
            if (side < 0 || side > 1) return;
            mem.WriteMemory<int>(visuals + ((ulong)side * 0x460) + 0xCC, value);
            mem.WriteMemory<int>(visuals + ((ulong)side * 0x460) + 0x27C, value);
            mem.WriteString(visuals + ((ulong)side * 0x460) + 0x428, costume);
        }

        private bool DVKCancelRequirements(ulong MOVESET)
        {
            int[,] reqs = FindInReqList("KAZUYA");
            if(!RemoveRequirements(MOVESET, reqs)) return false;

            int Co_Dummy_00 = GetMoveID(MOVESET, "Co_Dummy_00\0", 563);
            int Co_Dummy_00_cancel_idx = GetMoveAttributeIndex(MOVESET, Co_Dummy_00, (int)Offsets.cancel_addr);
            if (Co_Dummy_00 < 0) return true;   // It means already written

            int Kz_vipLP = GetMoveID(MOVESET, "Kz_vipLP\0", 1400);
            if (Kz_vipLP < 0) return false;
            int Kz_majin_00 = GetMoveID(MOVESET, "Kz_majin_00\0", 1400);
            if (Kz_majin_00 < 0) return false;
            // Writing into group cancel for Ultimate Rage Art
            long[,] arr = new long[,]
            {
                {1720, -1, -1, -1, -1, -1, -1, Co_Dummy_00, -1} // 1566 + 154 // 1566 + 154
            };
            if (!Edit_Cancels(MOVESET, arr, 1)) return false;

            int[] arr1 = new int[]
            {
                GetMoveID(MOVESET, "Kz_RageArts00\0", 1400), // To, From is fixed to Co_Dummy_00 (838)
            	Kz_majin_00,
                Kz_vipLP
            };
            // Copying move "RageArt00" (2103) to "Co_Dummy_00" (838)
            // Copying move "Kz_majin_00" (1658) to "Co_Dummy_02" (839)
            // Copying move "Kz_vipLP" (1600) to "Co_Dummy_03" (840)
            if (!CopyMoves(MOVESET, arr1, Co_Dummy_00)) return false;

            int ind1 = Co_Dummy_00_cancel_idx; // Cancel list index for Co_Dummy_00
            long[,] cancel_list = new long[,]
            {
		        // For Ultimate Rage Art
		        {ind1++, 0, 0, 11, 1, 1, 1, GetMoveID(MOVESET, "SKz_RageArts01Treasure_7CS\0", 2000), 65},
                {ind1++, 0x8000, 0, 0, 0, 0, 0, 32769, 336},
		        // For d/f+2,1 cancel
                {ind1++, 0, FindIndInList("KAZUYA",-1) + 4, 52, 23, 23, 23, Kz_vipLP, 65}, // 3555 + 4
                {ind1++, 0, FindIndInList("KAZUYA",-2), 23, 1, 32767, 1, 32769, 257}, // 3516
                {ind1++, 0, FindIndInList("KAZUYA",-3), 23, 1, 32767, 1, 32769, 257}, // 3410
                {ind1++, 0x8000, 0, 0, 46, 32767, 46, 32769, 336},
		        // For f+1+2,2 cancel
		        {ind1++, 0, FindIndInList("KAZUYA",-4), 23, 1, 32767, 1, 32769, 257}, // 1882
                {ind1++, 0, FindIndInList("KAZUYA",-5), 11, 1, 32767, 1, Kz_vipLP+1, 65}, // 3191
                {ind1++, 0, 0, 16, 32, 32, 32, GetMoveID(MOVESET, "Kz_bdyTuki\0", 1400), 65},
                {ind1++, 0x8000, 0, 0, 58, 32767, 58, 32769, 336},
		        // For f+1+2,2 cancel (blending)
		        {GetMoveAttributeIndex(MOVESET, Kz_vipLP, (int)Offsets.cancel_addr) + 8, 0x4000000200000000, 0, 11, 1, 24, 24, Co_Dummy_00 + 2, 80},
		        // For d/f+2,1 cancel (blending)
		        {GetMoveAttributeIndex(MOVESET, Kz_majin_00, (int)Offsets.cancel_addr) + 8, 0x4000000100000000, 0, 11, 1, 13, 13, Co_Dummy_00 + 1, 80},
		        // For Stopping Story Rage Art from Coming out (cancel list: 10177, entry 2)
		        {GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET, "SKz_RageArts_sp_nRv3\0", 2000), (int)Offsets.cancel_addr) + 1, -1, FindIndInList("KAZUYA",-6), -1, -1, -1, -1, -1, -1} // 3624
            };

            // Updating cancel lists
            if (!Edit_Cancels(MOVESET, cancel_list, 0)) return false;

            // Adjusting cancel lists
            reqs = new int[,]
            {
                {Co_Dummy_00+1, Co_Dummy_00_cancel_idx + 2} // Co_Dummy_02 (839), Index number to be assigned
            };
            if (!AssignMoveAttributeIndex(MOVESET, reqs, (int)OFFSETS.cancel_list)) return false;

            reqs = new int[,]
            {
                { Kz_vipLP, GetMoveAttributeIndex(MOVESET, Kz_vipLP, (int)Offsets.hit_cond_addr) + 2 },
            };
            // Adjusting hit condition
            if (!AssignMoveAttributeIndex(MOVESET, reqs, (int)OFFSETS.hit_condition)) return false;
            return true; // Memory has been successfully editied
        }

        private bool ASHCancelRequirements(ulong MOVESET)    // Ascended Heihachi requirements
        {
            // Editing requirements
            int[,] reqs = FindInReqList("HEIHACHI");
            if (!RemoveRequirements(MOVESET, reqs)) return false;

            int Co_Dummy_00 = GetMoveID(MOVESET, "Co_Dummy_00\0", 800);
            int Co_Dummy_00_cancel_idx = GetMoveAttributeIndex(MOVESET, Co_Dummy_00, (int)Offsets.cancel_addr);
            if (Co_Dummy_00 < 0) return true; // Already written

            // Writing into group cancels
            long[,] arr = new long[,]
            {
                {899, -1, -1, -1, -1, -1, -1, GetMoveID(MOVESET, "He_WK00F_7CS\0", 1600), -1},
                {1674, -1, -1, -1, -1, -1, -1, Co_Dummy_00, -1}  //(1541 + 133)
            };
            if (!Edit_Cancels(MOVESET, arr, 1)) return false;

            // This array is for copying moves
            int[] arr1 = new int[]
            {
                GetMoveID(MOVESET, "He_RageArts00\0", 1600) // He_RageArt00
            };

            if (!CopyMoves(MOVESET, arr1, Co_Dummy_00)) return false;

            int He_m_k01M_CS = GetMoveID(MOVESET, "He_m_k01M_CS\0", 1600);
            int He_m_k02M_CS = GetMoveID(MOVESET, "He_m_k02M_CS\0", 1600);
            if (He_m_k01M_CS < 0 || He_m_k02M_CS < 0) return false;

            int ind1 = Co_Dummy_00_cancel_idx;
            int ind2 = GetMoveAttributeIndex(MOVESET, He_m_k02M_CS, (int)Offsets.cancel_addr); // Cancel list of Spinning Demon kick 3 (boss version), idx 0
            if (ind2 < 0) return false;
            // Updating cancel lists
            // {index, command, req_idx, ext_idx, w_start, w_end, starting_frame, move, option}
            arr = new long[,]
            {
                // For Ultimate Rage Art
		        {ind1++, 0, 0, 11, 7, 7, 7, GetMoveID(MOVESET,"He_RageArts01_Treasure_7CS\0", 1600), 65},
                {ind1++, 0x8000, 0, 0, 0, 0, 0, 32769, 336},
		        // For Spinning Demon (kick 1)
		        {ind1++, 0x4000000100000000, 0, 11, 1, 15, 15, GetMoveID(MOVESET,"He_m_k00AG\0", 1600), 80},
                {ind1++, 0x400000080000004E, 0, 16, 1, 16, 16, He_m_k01M_CS, 80},
                {ind1++, 0x4000000800000000, 0, 11, 1, 15, 15, GetMoveID(MOVESET,"He_m_k00DG\0", 1600), 80},
                {ind1++, 0x8000, 0, 0, 49, 32767, 49, 32769, 336},
		        // For Spinning Demon (kick 2)
		        {ind1++, 0x400000080000004E, 0, 16, 1, 24, 24, He_m_k02M_CS, 80},
                {ind1++, 0x4000000100000000, 0, 11, 1, 16, 16, GetMoveID(MOVESET,"He_m_k01MAG\0", 1600), 80},
                {ind1++, 0x4000000800000020, 0, 11, 1, 23, 23, GetMoveID(MOVESET,"He_m_k01MDG\0", 1600), 80},
                {ind1++, 0x8000, 0, 0, 59, 32767, 59, 32769, 336},
		        // For Spinning Demon (kick 3)
		        {ind2 + 0, 0x4000000100000000, 0, -1, 1, -1, -1, -1, 80},
                {ind2 + 1, 0x4000000800000020, 0, -1, 1, -1, -1, -1, 80},
                {ind2 + 2, 0x400000080000004e, 0, -1, 1, -1, -1, -1, 80},
		        // For Spinning Demon (kick 4)
		        {ind2 + 4, 0x4000000100000000, 0, -1, 1, -1, -1, -1, 80},
                {ind2 + 5, 0x4000000800000020, 0, -1, 1, -1, -1, -1, 80},
                {ind2 + 6, 0x400000080000004e, 0, -1, 1, -1, -1, -1, 80},
		        // For Spinning Demon (kick 5)
		        {ind2 + 8, 0x4000000100000000, 0, -1, 1, -1, -1, -1, 80},
                {ind2 + 9, 0x4000000800000020, 0, -1, 1, -1, -1, -1, 80},
                {ind2 +10, 0x400000080000004e, 0, -1, 1, -1, -1, -1, 80},
		        // For Spinning Demon (kick 6)
		        {ind2 +12, 0x4000000100000000, 0, -1, 1, -1, -1, -1, 80},
                {ind2 +13, 0x4000000800000020, 0, -1, 1, -1, -1, -1, 80},
		        // From Regular Spinning Demon to boss version
		        {ind2 +15, -1, 0, -1, -1, -1, -1, -1, -1}
            };
            if (!Edit_Cancels(MOVESET, arr, 0)) return false;

            reqs = new int[,]
            {
                {GetMoveID(MOVESET,"He_m_k00_CS\0", 1600), Co_Dummy_00_cancel_idx + 2}, // For Spinning Demon Kick 1, 4244
		        {He_m_k01M_CS, Co_Dummy_00_cancel_idx + 6}  // For Spinning Demon Kick 2, 4248
            };
            if (!AssignMoveAttributeIndex(MOVESET, reqs, (int)OFFSETS.cancel_list)) return false;

            if (!HeihachiAura(MOVESET, GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET, "He_sFUN00_", 1200), (int)Offsets.ext_prop_addr))) return false;

            return true; // Successfully Written
        }

        private bool SHACancelRequirements(ulong MOVESET) // For Shin Akuma
        {
            // For removing requirements from cancels
            int[,] arr = FindInReqList("AKUMA");
            if (!RemoveRequirements(MOVESET, arr)) return false;

            // For extra move properties
            // {MoveID, Extraprop index value to be assigned to it}
            int Mx_asyura = GetMoveID(MOVESET, "Mx_asyura\0", 1900);
            int Mx_asyura2 = GetMoveID(MOVESET, "Mx_asyura2\0", 1900);
            int Mx_asyurab = GetMoveID(MOVESET, "Mx_asyurab\0", 1900);
            arr = new int[,]
            {
                {Mx_asyura,  GetMoveAttributeIndex(MOVESET, Mx_asyura + 2,  (int)Offsets.ext_prop_addr)},
                {Mx_asyura2, GetMoveAttributeIndex(MOVESET, Mx_asyura2 + 2, (int)Offsets.ext_prop_addr)},
                {Mx_asyurab, GetMoveAttributeIndex(MOVESET, Mx_asyurab + 1, (int)Offsets.ext_prop_addr)}
            };
            if (!AssignMoveAttributeIndex(MOVESET, arr, (int)OFFSETS.extraprops)) return false;

            long[,] arr1 = new long[,]
            {
                {GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET,"Mx_RageArtsL_n\0", 1900), (int)Offsets.cancel_addr) + 2, -1, 0, -1, -1, -1, -1, -1, -1}, // Cancel to Rage Art finish L -> Treasure RA
                {GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET,"Mx_RageArtsR_n\0", 1900), (int)Offsets.cancel_addr) + 2, -1, 0, -1, -1, -1, -1, -1, -1}, // Cancel to Rage Art finish R -> Treasure RA
            };
            
            // Updating cancel lists
            if (!Edit_Cancels(MOVESET, arr1, 0)) return false;

            int Mx_EXrecover_7CS = GetMoveID(MOVESET, "Mx_EXrecover_7CS\0", 1900);
            if (Mx_EXrecover_7CS < 0) return false;

            // Writing into group cancels
            arr1 = new long[,]
            {
                {588, -1, -1, -1, -1, -1, -1, Mx_EXrecover_7CS, -1}, // 583+5 - for d+3+4 meter charge
                {768, -1, -1, -1, -1, -1, -1, Mx_EXrecover_7CS, -1}, // 763+5 - for d+3+4 meter charge
            };
            if (!Edit_Cancels(MOVESET, arr1, 1)) return false;

            return true;  // This means the moveset has been modified successfully
        }

        private bool JINCancelRequirements(ulong MOVESET)    // Asura Jin requirements
        {
            // Editing requirements
            int[,] reqs = new int[,]
            {
                {1100, 3}, // Zen into ETU
		        {2230, 3}, // D+1+2 Slide (1)
		        {2236, 3}, // D+1+2 Slide (2)
		        {2252, 3}, // Slide Player forward (1)
		        {2258, 3}, // Slide Player forward (2)
		        {2279, 3}, // Slide Player forward during ULLRK
		        {2306, 3}, // Slide Player forward during UEWHF
		        {2342, 3}, // Slide Player forward during UETU
		        {3418, 3}, // Intro
		        {3423, 3}  // Outro
            };
            if (!RemoveRequirements(MOVESET, reqs)) return false;

            int Co_Dummy_00 = GetMoveID(MOVESET, "Co_Dummy_00\0", 700);
            int Co_Dummy_00_cancel_idx = GetMoveAttributeIndex(MOVESET, Co_Dummy_00, (int)Offsets.cancel_addr);
            if (Co_Dummy_00 < 0) return true;
            // Writing into group cancels
            //arr = new long[,]
            //{
            //    {1546, 838} // 1566 + 154
            //};
            //if (!EditGroupCancels(MOVESET, arr, arr.GetLength(0)))
            //    return;

            int back1 = GetMoveID(MOVESET, "Jz_hadohB00_7CS\0", 1400); // b+1
            int down12 = GetMoveID(MOVESET, "Jz_zansin00_7CS\0", 1400); // d+1+2
            int standing4 = GetMoveID(MOVESET, "Jz_round_RKbackE\0", 1400); // standing 4 on hit
            int whf_h = GetMoveID(MOVESET, "Jz_shoryu34f2ph\0", 1400); // WHF on hit
            int UEWHF = GetMoveID(MOVESET, "Jz_shoryu24_7CS\0", 1400); // UEWHF
            if (back1 < 0 || down12 < 0 || standing4 < 0 || whf_h < 0 || UEWHF < 0) return false; // Error checking
            // This array is for copying moves
            int[] moveIDs = new int[]
            {
                back1, // For b+1 into d/f+1
		        back1, // For b+1 into d/f+2
		        back1, // For b+1 into d/f+4
		        down12, // For d+1+2 into 1+2
		        down12, // For d+1+2 into 3+4
		        down12, // For d+1+2 into 2
		        down12, // For d+1+2 into 4
		        down12, // For d+1+2 into 3
		        standing4, // For Standing 4 into UEWHF
		        whf_h  // For WHF > UEWHF
	        };

            int ind1 = Co_Dummy_00_cancel_idx; // Cancel list index of Co_Dummy_00
            int ind2 = GetMoveAttributeIndex(MOVESET, down12, (int)Offsets.cancel_addr) + 2;  // Cancel list index of d+1+2, idx 3
            int ind3 = GetMoveAttributeIndex(MOVESET, back1, (int)Offsets.cancel_addr) + 2;  // Cancel list index of boss b+1, idx 3
            int ind4 = GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET, "Jz_3lklk_zansin\0", 1400), (int)Offsets.cancel_addr); // Cancel list index of 1,3 / d/f+3,3 ZEN cancel, idx 1
            int ind5 = GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET, "Jz_rasetuzansin\0", 1400), (int)Offsets.cancel_addr); // Cancel list index of b,f+2,3 ZEN cancel, idx 1
            int ind6 = GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET, "Jz_oni8_zansin\0", 1400), (int)Offsets.cancel_addr); // Cancel list index of ws+1,2 ZEN cancel, idx 1
            int ind7 = GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET, "Jz_4lk_zansin\0", 1400), (int)Offsets.cancel_addr); // Cancel list index of b+3 ZEN cancel, idx 1
            int ind8 = GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET, "Jz_6rk_zansin2\0", 1400), (int)Offsets.cancel_addr); // Cancel list index of f+4 ZEN cancel, idx 1
            if (ind4 < 0 || ind5 < 0 || ind6 < 0 || ind7 < 0 || ind8 < 0) return false;

            // {index, command, req_idx, ext_idx, w_start, w_end, starting_frame, move, option}
            long[,] arr1 = new long[,]
            {
                // For b+1 into d/f+1
		        {ind1++, 0, 0, 20, 30, 30, 30, GetMoveID(MOVESET, "Jz_dslpS_7CS\0", 1400), 65},
                {ind1++, 0x8000, 0, 0, 55, 32767, 55, 32769, 336},
		        // For b+1 into d/f+2
		        {ind1++, 0, 0, 20, 30, 30, 30, UEWHF, 65},
                {ind1++, 0x8000, 0, 0, 55, 32767, 55, 32769, 336},
		        // For b+1 into d/f+4
		        {ind1++, 0, 0, 20, 30, 30, 30, GetMoveID(MOVESET, "Jz_m_k20_7CS\0", 1400), 65},
                {ind1++, 0x8000, 0, 0, 55, 32767, 55, 32769, 336},
		        // For d+1+2 into 1+2
		        {ind1++, 0, 0, 13, 25, 25, 25, GetMoveID(MOVESET, "Jz_zan_lrp\0", 1400), 65},
                {ind1++, 0x8000, 0, 0, 45, 32767, 45, 32769, 336},
		        // For d+1+2 into 3+4
		        {ind1++, 0, 0, 13, 25, 25, 25, GetMoveID(MOVESET, "Jz_jmplk\0", 1400), 65},
                {ind1++, 0x8000, 0, 0, 45, 32767, 45, 32769, 336},
		        // For d+1+2 into 2
		        {ind1++, 0, 0, 13, 25, 25, 25, GetMoveID(MOVESET, "Jz_zan_yokerp\0", 1400), 65},
                {ind1++, 0x8000, 0, 0, 45, 32767, 45, 32769, 336},
		        // For d+1+2 into 4
		        {ind1++, 0, 0, 13, 25, 25, 25, GetMoveID(MOVESET, "Jz_zan_srk00EX\0", 1400), 65},
                {ind1++, 0x8000, 0, 0, 45, 32767, 45, 32769, 336},
		        // For d+1+2 into 3
		        {ind1++, 0, 0, 13, 25, 25, 25, GetMoveID(MOVESET, "Jz_2lrplk\0", 1400), 65},
                {ind1++, 0x8000, 0, 0, 45, 32767, 45, 32769, 336},
		        // For standing 4 into UEWHF
		        {ind1++, 0, 0, 15, 40, 40, 40, UEWHF, 65},
                {ind1++, 0x8000, 0, 0, 40, 32767, 40, 32769, 336},
		        // For WHF into UEWHF
		        {ind1++, 0, 0, 15, 36, 36, 36, UEWHF, 65},
                {ind1++, 0x8000, 0, 0, 38, 32767, 38, 32769, 336},
		        // d+1+2 (boss version) cancel list
		        {ind2++, 0x4000000300000000, 0, 10, 16, 24, 24, Co_Dummy_00 + 3, 80},
                {ind2++, 0x4000000C00000000, 0, 10, 16, 24, 24, Co_Dummy_00 + 4, 80},
                {ind2++, 0x4000000200000000, 0, 10, 16, 24, 24, Co_Dummy_00 + 5, 80},
                {ind2++, 0x4000000800000000, 0, 10, 16, 24, 24, Co_Dummy_00 + 6, 80},
                {ind2++, 0x4000000400000000, 0, 10, 16, 24, 24, Co_Dummy_00 + 7, 80},
		        // b+1 Cancel list
		        {GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET, "Jz_hadohB00\0", 1400), (int)Offsets.cancel_addr) + 1, 0, 0, 10, 1, 1, 1, GetMoveID(MOVESET,"Jz_hadohB00_7CS\0", 1400), 65},
		        // b+1 (boss version) cancel list
		        {ind3++, 0x4000000100000000, 0, 10, 16, 29, 29, Co_Dummy_00 + 0, 80},
                {ind3++, 0x4000000200000000, 0, 10, 16, 29, 29, Co_Dummy_00 + 1, 80},
                {ind3++, 0x4000000800000000, 0, 10, 16, 29, 29, Co_Dummy_00 + 2, 80},
		        // For UEWHF into UEWHF
		        {GetMoveAttributeIndex(MOVESET, UEWHF, (int)Offsets.cancel_addr) + 5, 0x4000000200000040, 0, -1, 24, -1, -1, -1, 80},
		        // Standing 4 cancel list
		        {GetMoveAttributeIndex(MOVESET, standing4, (int)Offsets.cancel_addr), 0x4000000200000040, 0, 10, 1, 39, 39, Co_Dummy_00 + 8, 80},
		        // For d/f+4,4 / 1,3 cancel list
		        {ind4++, 0x4000000100000008, 0, -1, 1, -1, -1, -1, 80},
                {ind4++, 0x4000000200000008, 0, -1, 1, -1, -1, -1, 80},
                {ind4++, 0x4000000800000008, 0, -1, 1, -1, -1, -1, 80},
		        // For b,f+2,3 cancel list
		        {ind5++, 0x4000000100000008, 0, -1, 1, -1, -1, -1, 80},
                {ind5++, 0x4000000200000008, 0, -1, 1, -1, -1, -1, 80},
                {ind5++, 0x4000000800000008, 0, -1, 1, -1, -1, -1, 80},
		        // For ws+1,2 cancel list
		        {ind6++, 0x4000000100000008, 0, -1, 1, -1, -1, -1, 80},
                {ind6++, 0x4000000200000008, 0, -1, 1, -1, -1, -1, 80},
                {ind6++, 0x4000000800000008, 0, -1, 1, -1, -1, -1, 80},
		        // For b+3 cancel list
		        {ind7++, 0x4000000100000008, 0, -1, 1, -1, -1, -1, 80},
                {ind7++, 0x4000000200000008, 0, -1, 1, -1, -1, -1, 80},
                {ind7++, 0x4000000800000008, 0, -1, 1, -1, -1, -1, 80},
		        // For f+4 cancel list
		        {ind8++, 0x4000000100000008, 0, -1, 1, -1, -1, -1, 80},
                {ind8++, 0x4000000200000008, 0, -1, 1, -1, -1, -1, 80},
                {ind8++, 0x4000000800000008, 0, -1, 1, -1, -1, -1, 80},
		        // For WHF into Dummy Copy
		        {GetMoveAttributeIndex(MOVESET, whf_h, (int)Offsets.cancel_addr), 0x4000000200000040, 0, 10, 1, 35, 35, Co_Dummy_00 + 9, 80},
		        // For EWHF into UEWHF
		        {GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET, "Jz_shoryu24\0", 1400), (int)Offsets.cancel_addr) + 5, 0x4000000200000040, 0, -1, 1, -1, -1, -1, 80}
            };
            // Updating cancel lists
            if (!Edit_Cancels(MOVESET, arr1, 0)) return false;

            // Copying Moves
            if (!CopyMoves(MOVESET, moveIDs, Co_Dummy_00)) return false;

            ind1 = Co_Dummy_00;  // ID of Co_Dummy_00 move
            ind2 = Co_Dummy_00_cancel_idx; // Cancel Index
            reqs = new int[,]
            {
                {ind1++, ind2 + 0}, // {Co_Dummy_00 (841), 4210}
		        {ind1++, ind2 + 2}, // {Co_Dymmy_02 (842), 4212}
		        {ind1++, ind2 + 4}, // {Co_Dymmy_03 (843), 4214}
		        {ind1++, ind2 + 6}, // {Co_Dymmy_05 (844), 4216}
		        {ind1++, ind2 + 8}, // {Co_Dymmy_06 (845), 4218}
		        {ind1++, ind2 +10}, // {Co_Dymmy_07 (846), 4219}
		        {ind1++, ind2 +12}, // {Co_Dymmy_08 (847), 4220}
		        {ind1++, ind2 +14}, // {Co_Dymmy_09 (848), 4222}
		        {ind1++, ind2 +16}, // {Co_Dymmy_10 (849), 4224}
		        {ind1++, ind2 +18}  // {Co_Dymmy_11 (850), 4226}
            };
            if (!AssignMoveAttributeIndex(MOVESET, reqs, (int)OFFSETS.cancel_list)) return false;

            return true; // Memory has been successfully modified
        }

        private bool BS7CancelRequirements(ulong MOVESET) // For Devil Kazumi
        {
            // For removing requirements from cancels
            // {RequirementIndex, how many requirements to zero}
            int[,] arr = new int[,]
            {
                {24, 3},   // Juggle escape
                {2043, 3}, // 1,1,2
                {2532, 3}, // Intro
                {2537, 3}, // Outro
            };
            if (!RemoveRequirements(MOVESET, arr)) return false;

            return true;  // This means the moveset has been modified successfully
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// UTILITY FUNCTIONS
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // This function checks if the moveset exists or not
        private bool MovesetExists(ulong addr)
        {
            if (mem.ReadMemory<int>(addr) == 0x10000) return true;
            return false;
        }        
        // This function checks/unchecks the check boxes
        private void Checkboxes_checks(bool value)
        {
            for(int i = 0; i < 5; i++)
            {
                Checkbox_Set(i + 1, value);
            }
        }
        private void Button_quit_Click(object sender, EventArgs e)
        {
            CloseProgram();
        }
        private void CloseProgram()
        {
            Checkboxes_checks(false);
            fileData.Clear();
            requirements.Clear();
            Application.Exit();
        }

        // FUNCTIONS I CREATED IN THE C++ TRAINER
        int GameMode()
        {
            return mem.ReadMemory<int>(visuals + 0x10);
        }
        int CheckPlayerSide()
        {
            return mem.ReadMemory<int>(visuals + 0x14);
        }
        // Returns the ID of currently selected character for given side
        private int GetCharID(int side)
        {
            if (side < 0 || side > 1) return 255;
            return mem.ReadMemory<int>(visuals + ((ulong)side * 0x4) + 0x1C);
        }
        // Returns the ID of currently selected character from inside a character's movelist
        private int GetCharID(ulong moveset)
        {
            return (int)mem.ReadMemory<short>(moveset + 0x14E);
        }
        // Returns the ID of currently selected character for given side
        private int GetCostumeID(int side)
        {
            if (side < 0 || side > 1) return 255;
            return mem.ReadMemory<int>(visuals + ((ulong)side * 0x460) + 0xCC);
        }
        // This function loads given character
        private void LoadCharacter(int side, int ID)
        {
            if (side < 0 || side > 1) return;
            else if (ID < 0 || ID > 56) return;
            mem.WriteMemory<int>(visuals + ((ulong)side * 0x4) + 0x1C, ID);
            //mem.WriteMemory<int>(p1profileStruct + 0x10, ID);
        }
        int GetMoveID(ulong moveset, string moveName, int starting_index = 0)
        {
            ulong moves_addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.moves);
            if (moves_addr == 0) return -1;
            ulong moves_size = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.moves + 0x8);
            if (moves_size == 0) return -1;
            ulong addr, moveNameAddr;
            string moveNameRead;
            if (starting_index < 0) starting_index = 0;
            for (int i = starting_index; i < (int)moves_size; i++)
            {
                addr = moves_addr + (ulong)(i * 176);
                moveNameAddr = (ulong)mem.ReadMemory<ulong>(addr);
                if (moveNameAddr == 0) return -1;
                moveNameRead = mem.ReadMemoryString(moveNameAddr, moveName.Length);
                if (moveNameRead == string.Empty) return -1;
                if (moveName.CompareTo(moveNameRead) == 0) return i;
            }
            return -1;
        }
        int GetMoveAttributeIndex(ulong moveset, int moveID, int offset)
        {
            if (moveID < 0) return -1;
            ulong moves_addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.moves);
            if (moves_addr == 0) return -1;
            ulong moves_size = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.moves + 0x8);
            if (moves_size == 0) return -1;
            ulong addr = moves_addr + (ulong)(moveID * 176);
            ulong attr_addr = mem.ReadMemory<ulong>(addr + (ulong)offset);
            int index;
            if (offset == (int)Offsets.cancel_addr)
            {
                addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.cancel_list);
                if (addr == 0) return -1;
                index = (int)(attr_addr - addr) / 40;
            }
            else if (offset == (int)Offsets.hit_cond_addr)
            {
                addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.hit_condition);
                if (addr == 0) return -1;
                index = (int)(attr_addr - addr) / 24;
            }
            else if (offset == (int)Offsets.ext_prop_addr)
            {
                addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.extraprops);
                if (addr == 0) return -1;
                index = (int)(attr_addr - addr) / 12;
            }
            else index = -1;
            return index;
        }
        bool RemoveRequirements(ulong moveset, int[,] arr)
        {
            if (arr == null) return true;
            ulong requirements_addr = mem.ReadMemory<ulong>(moveset + 0x160);
            if (requirements_addr == 0) return false; // Return in case of null
            ulong addr, n_addr;
            int rows = arr.GetLength(0);
            // Removing requirements from the given array
            for (int i = 0; i < rows; i++)
            {
                addr = requirements_addr + (8 * (ulong)arr[i,0]);
                // Writing and replacing the code to make the HUD comeback and stop AI from reverting Devil Transformation
                if (arr[i,1] == 0 && GetCharID(moveset) == 9)
                {
                    if (!mem.WriteMemory<int>(addr, 563)) return false;
                    if (!mem.WriteMemory<int>(addr + 16, 0x829D)) return false;
                    if (!mem.WriteMemory<int>(addr + 20, 1)) return false;
                }
                // Handling the requirements to allow Akuma's parry
                else if (arr[i,1] == 0 && GetCharID(moveset) == 32)
                {
                    if (!mem.WriteMemory<int>(addr + 32, 0)) return false;
                    if (!mem.WriteMemory<int>(addr + 36, 0)) return false;
                    if (!mem.WriteMemory<int>(addr + 64, 0)) return false;
                    if (!mem.WriteMemory<int>(addr + 68, 0)) return false;
                    arr[i, 1] = 3;
                }
                for (int j = 0; j < arr[i,1]; j++)
                {
                    n_addr = addr + (ulong)(8 * j);
                    if (!mem.WriteMemory<int>(n_addr, 0)) return false;
                }
            }
            return true;
        }

        int FindReqIdx(ulong moveset, int[] arr)
        {
            if (arr == null) return -1;
            if (arr.GetLength(0) % 2 != 0) return -1;
            ulong requirements_addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.requirements);
            if (requirements_addr == 0) return -1;
            int rows = mem.ReadMemory<int>(moveset + (int)OFFSETS.requirements + 8);
            if (rows == 0) return -1;
            int n = arr.GetLength(0);
            ulong addr = requirements_addr;
            bool matched = false;
            int value;
            for (int i = 0; i < rows; i++)
            {
                value = mem.ReadMemory<int>(addr);
                if (value == arr[0])
                {
                    matched = true;
                    for (int j = 0; j < n; j++)
                    {
                        value = mem.ReadMemory<int>(addr);
                        if (value != arr[j])
                        {
                            matched = false;
                            break;
                        }
                        addr += 4;
                    }
                }
                if (matched) break;
                else addr += 4;
            }
            if (!matched) return -1;
            value = (int)((addr - requirements_addr) / 8);
            Debug.WriteLine("Index = " + value.ToString());
            return value;
        }

        bool CopyMoves(ulong moveset, int[] arr, int Co_Dummy)
        {
            ulong moves_addr = mem.ReadMemory<ulong>(moveset + 0x210);
            if (moves_addr == 0) return false;
            ulong FromMove, ToMove;
            int abc, rows = arr.GetLength(0);
            for (int i = 0; i < rows; i++)
            {
                if (arr[i] < 0) return false;
                FromMove = moves_addr + (ulong)(176 * arr[i]);
                ToMove = moves_addr + (ulong)(176 * (Co_Dummy + i));
                Debug.WriteLine(string.Format("{0:X}", ToMove));
                for (int j = 0; j < 176 / 4; j++)
                {
                    if (j * 4 == 32) continue;
                    abc = mem.ReadMemory<int>(FromMove + (ulong)(j * 4));
                    //if (abc == 0) return false;
                    if (!mem.WriteMemory<int>(ToMove + (ulong)(j * 4), abc)) return false;
                }
            }
            return true;
        }

        // FLAG Value 0 = Edit Normal cancels. Value 1 = Edit Group cancels
        bool Edit_Cancels(ulong moveset, long[,] arr, int FLAG)
        {
            ulong cancel_addr, requirement, extradata;
            if (FLAG == 0)
            {
                cancel_addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.cancel_list);
                if (cancel_addr == 0) return false;
            }
            else
            {
                cancel_addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.group_cancel_list);
                if (cancel_addr == 0) return false;
            }
            requirement = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.requirements);
            if (requirement == 0) return false;
            extradata = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.cancel_extra);
            if (extradata == 0) return false;

            ulong addr, req, ext;
            int rows = arr.GetLength(0);
            for (int i = 0; i < rows; i++)
            {
                // Reaching Address
                addr = cancel_addr + (ulong)(arr[i,0] * 40); // 1 cancel is of 40 bytes
                // Command
                if (arr[i, 1] != -1)
                    if (!mem.WriteMemory<ulong>(addr, (ulong)arr[i,1])) return false;
                // Requirement address
                if (arr[i, 2] != -1)
                {
                    req = requirement + (8 * (ulong)arr[i,2]); // 1 requirement field is of 8 bytes
                    if (!mem.WriteMemory<ulong>(addr + 8, req)) return false;
                }
                // Extradata address
                if (arr[i,3] != -1)
                {
                    ext = extradata + (4 * (ulong)arr[i,3]); // 1 Extradata field is of 4 bytes
                    if (!mem.WriteMemory<ulong>(addr + 16, ext)) return false;
                }
                // Frame window start, end & starting frame
                if (arr[i,4] != -1) if (!mem.WriteMemory<int>(addr + 24, (int)arr[i, 4])) return false;
                if (arr[i,5] != -1) if (!mem.WriteMemory<int>(addr + 28, (int)arr[i, 5])) return false;
                if (arr[i,6] != -1) if (!mem.WriteMemory<int>(addr + 32, (int)arr[i, 6])) return false;
                
                // Cancel move
                if (arr[i,7] != -1) if (!mem.WriteMemory<short>(addr + 36, (short)arr[i, 7])) return false;
                // Cancel option
                if (arr[i,8] != -1) if (!mem.WriteMemory<short>(addr + 38, (short)arr[i, 8])) return false;
            }
            return true;
        }

        bool AssignMoveAttributeIndex(ulong moveset, int[,] arr, int offset)
        {
            ulong moves_addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.moves);
            if (moves_addr == 0) return false;
            ulong attribute = mem.ReadMemory<ulong>(moveset + (ulong)offset);
            if (attribute == 0) return false;
            ulong addr, idx, off;
            int rows = arr.GetLength(0);
            for (int i = 0; i < rows; i++)
            {
                // Getting address of the move
                addr = moves_addr + (ulong)(176 * arr[i,0]);
                // Getting address of the attribute
                if (offset == (int)OFFSETS.cancel_list)
                {
                    idx = attribute + (ulong)(40 * arr[i,1]);
                    off = (int)Offsets.cancel_addr;
                }
                else if (offset == (int)OFFSETS.extraprops)
                {
                    idx = attribute + (ulong)(12 * arr[i, 1]);
                    off = (int)Offsets.ext_prop_addr;
                }
                else if (offset == (int)OFFSETS.hit_condition)
                {
                    idx = attribute + (ulong)(24 * arr[i, 1]);
                    off = (int)Offsets.hit_cond_addr;
                }
                else return false;
                // Writing to the memory at the address
                if (!mem.WriteMemory<ulong>(addr + off, idx)) return false;
            }
            return true;
        }

        bool HeihachiAura(ulong moveset, int index)
        {
            ulong extraprops_addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.extraprops);
            if (extraprops_addr == 0) return false;
            ulong addr = extraprops_addr + (ulong)(12 * (index - 1));
            if (!mem.WriteMemory<int>(addr + 0, 8001)) return false;
            if (!mem.WriteMemory<int>(addr + 4, 0x829d)) return false;
            if (!mem.WriteMemory<int>(addr + 8, 1)) return false;
            return true;
        }

        private void Panels_Visibility(bool value)
        {
            panel_instructions.Visible = value;
            panel_kazuya.Visible = value;
            panel_heihachi.Visible = value;
            panel_akuma.Visible = value;
            panel_kazumi.Visible = value;
            panel_jin.Visible = value;
            button_back.Visible = value;
        }

        ///////////////////// FILE HANDLING FUNCTIONS //////////////////////////
        bool Parse(string str)
        {
            int offsets = 0;
            for (int i = 0; i < str.Length; i++)
                if (str[i] == ',')
                    offsets++;
            if (offsets == 0) return false;
            // Removing all white spaces
            str = str.Trim(); // From beginning and end
            str = String.Concat(str.Where(c => !Char.IsWhiteSpace(c))); // From middle
            string val1 = str.Substring(0, str.IndexOf('='));
            string remaining = str.Substring(str.IndexOf('=') + 1);
            ulong[] offsetsList = new ulong[offsets];
            string item;
            for (int i = 0; i < offsets; i++)
            {
                item = remaining.Substring(0, remaining.IndexOf(',')); // Reading an offset
                item = item.Substring(item.IndexOf('x') + 1); // Removing 0x from the beginning
                try { offsetsList[i] = UInt64.Parse(item, System.Globalization.NumberStyles.HexNumber); }
                catch (Exception ex)
                {
                    if (ex.HResult == -2146233033)
                        return false;
                    else throw ex;
                }
                remaining = remaining.Substring(remaining.IndexOf(',') + 1);
            }
            fileData.Add(new Node(val1, offsetsList));
            return true;
        }
        void ReadAddressesFromFile()
        {
            bool Error = false;
            string fileName = "addresses.txt";
            if (!File.Exists(fileName))
            {
                MessageBox.Show("Could not find and open the following file: addresses.txt\nClosing Program.");
                CloseProgram();
                return;
            }
            FileStream fs = new FileStream(fileName, FileMode.Open);
            StreamReader sr = new StreamReader(fs);
            sr.BaseStream.Seek(0, SeekOrigin.Begin);
            string str = "abc"; // Putting in random stuff so the string does not get cucked
            while (str != null)
            {
                str = sr.ReadLine();
                if (str == null) break;
                if (!Parse(str))
                {
                    MessageBox.Show("Invalid Data written in the file: addresses.txt\nClosing Program.");
                    CloseProgram(); sr.Close(); fs.Close();
                    return;
                }
            }
            sr.Close();
            fs.Close();

            fileName = "requirements.txt";
            if (!File.Exists(fileName))
            {
                MessageBox.Show("Could not find and open the following file: requirements.txt\nClosing Program.");
                CloseProgram();
                return;
            }
            fs = new FileStream(fileName, FileMode.Open);
            sr = new StreamReader(fs);
            sr.BaseStream.Seek(0, SeekOrigin.Begin);
            str = "abc"; // Putting in random stuff so the string does not get cucked
            int slash; // Stores index for the '/' in the string
            string charName;
            List<Node2> list = new List<Node2>();
            bool Write = false;
            while (str != null)
            {
                str = sr.ReadLine();
                if (str == null) break;
                if (str == string.Empty) continue;
                if (str[0] == '/') continue;
                slash = str.IndexOf('/');
                if (slash != -1) str = str.Substring(0, slash);
                str = str.Trim();
                str = String.Concat(str.Where(c => !Char.IsWhiteSpace(c)));
                // PARSING STRING
                if (Char.IsLetter(str[0]))
                {
                    charName = str; // Storing Name of the character
                    if (str.CompareTo("JIN") == 0)
                    {
                        Write = true;
                    }
                    else if (str.CompareTo("HEIHACHI") == 0)
                    {
                        Write = true;
                    }
                    else if (str.CompareTo("KAZUYA") == 0)
                    {
                        Write = true;
                    }
                    else if (str.CompareTo("KAZUMI") == 0)
                    {
                        Write = true;
                    }
                    else if (str.CompareTo("AKUMA") == 0)
                    {
                        Write = true;
                    }
                    else
                    {
                        MessageBox.Show("Invalid character label written in the file: requirements.txt\nClosing Program.");
                        CloseProgram(); sr.Close(); fs.Close(); Write = false;
                        return;
                    }

                    if (Write)
                    {
                        requirements.Add(new Reqs(charName, ToArray(list)));
                        int[,] req_list = FindInReqList(charName);
                        if (req_list != null)
                        {
                            //Debug.WriteLine(charName);
                            //for (int i = 0; i < req_list.GetLength(0); i++)
                            //    Debug.WriteLine(req_list[i, 0].ToString() + ", " + req_list[i, 1].ToString());
                        }
                        else
                        {
                            //MessageBox.Show("Could not read any requirements from the file: requirements.txt\nClosing Program.");
                            //CloseProgram(); sr.Close(); fs.Close();
                            //return;
                        }
                        list.Clear();
                    }
                }
                else // Reading a requirement
                {
                    if (!Char.IsDigit(str[0]))
                    {
                        MessageBox.Show("Invalid Index value written in the file: requirements.txt\nClosing Program.");
                        CloseProgram(); sr.Close(); fs.Close();
                        return;
                    }
                    // Reading requirements
                    string index, value;
                    int idx, val;
                    if (str.IndexOf(',') != -1)
                    {
                        index = str.Substring(0, str.IndexOf(','));
                        value = str.Substring(str.IndexOf(',') + 1);

                        try { idx = Int32.Parse(index); }
                        catch (Exception ex)
                        {
                            if (ex.HResult == -2146233033)
                            {
                                MessageBox.Show("Error occured when parsing index values from the file: requirements.txt\nClosing Program.");
                                CloseProgram(); sr.Close(); fs.Close();
                                return;
                            }
                            else throw ex;
                        }

                        try { val = Int32.Parse(value); }
                        catch (Exception ex)
                        {
                            if (ex.HResult == -2146233033)
                            {
                                MessageBox.Show("Error occured when parsing index values from the file: requirements.txt\nClosing Program.");
                                CloseProgram(); sr.Close(); fs.Close();
                                return;
                            }
                            else throw ex;
                        }

                        list.Add(new Node2(idx, val));
                    }
                    else
                    {
                        MessageBox.Show("Invalid Data written in the file: requirements.txt\nClosing Program.");
                        CloseProgram(); sr.Close(); fs.Close();
                        return;
                    }
                }
            }
            if (FindNameInReqList("JIN") == null) { Error = true; charName = "JIN"; }
            else if (FindNameInReqList("HEIHACHI") == null) { Error = true; charName = "HEIHACHI"; }
            else if (FindNameInReqList("KAZUYA") == null) { Error = true; charName = "KAZUYA"; }
            else if (FindNameInReqList("KAZUMI") == null) { Error = true; charName = "KAZUMI"; }
            else if (FindNameInReqList("AKUMA") == null) { Error = true; charName = "AKUMA"; }
            else { Error = false; charName = "NO ERROR"; }
            if (Error)
            {
                MessageBox.Show($"Could not read requirements for {charName} from file: requirements.txt\nClosing Program.");
                CloseProgram();
            }
            sr.Close();
            fs.Close();
        }
        private int[,] ToArray(List<Node2> list)
        {
            int size = list.Count;
            if (size <= 0) return null;
            int[,] arr = new int[size, 2];
            for(int i = 0; i < size; i++)
            {
                arr[i, 0] = list[i].index;
                arr[i, 1] = list[i].value;
            }
            return arr;
        }
        private ulong[] FindInList(string name)
        {
            foreach (Node a in fileData)
            {
                if (a.name == name) return a.ptr;
            }
            return null;
        }

        private int[,] FindInReqList(string name)
        {
            foreach (Reqs a in requirements)
            {
                if (a.name == name) return a.ptr;
            }
            return null;
        }
        private string FindNameInReqList(string name)
        {
            foreach (Reqs a in requirements)
            {
                if (a.name == name) return a.name;
            }
            return null;
        }

        private int FindIndInList(string name, int v)
        {
            if (v >= 0) return -1;
            foreach (Reqs a in requirements)
            {
                if (a.name == name)
                {
                    int size = a.ptr.GetLength(0);
                    for(int i = 0; i < size; i++)
                        if (a.ptr[i, 1] == v)
                            return a.ptr[i, 0];
                }
            }
            return -1;
        }
    }
}