using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using Memory.Win64;
using System.IO;

// TODO: Replacing long[,] array for storing cancels with Cancel[] arrays
// and Implementing better ways to find cancel indexes

namespace TekkenTrainer
{
    public partial class Form1 : Form
    {
        class Cancel
        {
            public int index;
            public ulong command;
            public int requirement_idx;
            public int extradata_idx;
            public int frame_window_start;
            public int frame_window_end;
            public int starting_frame;
            public short move_id;
            public short type;

            public Cancel()
            {
                index = requirement_idx = extradata_idx = frame_window_end = frame_window_start = starting_frame = -1;
                command = 0;
                move_id = type = -1;
            }

            public Cancel(ulong cmd, int rq, int ed, int fs, int fe, int sf, short mi, short ty)
            {
                index = -1;
                command = cmd;
                requirement_idx = rq;
                extradata_idx = ed;
                frame_window_start = fs;
                frame_window_end = fe;
                starting_frame = sf;
                move_id = mi;
                type = ty;
            }

            public Cancel(int idx, ulong cmd, int rq, int ed, int fs, int fe, int sf, short mi, short ty)
            {
                index = idx;
                command = cmd;
                requirement_idx = rq;
                extradata_idx = ed;
                frame_window_start = fs;
                frame_window_end = fe;
                starting_frame = sf;
                move_id = mi;
                type = ty;
            }

            public static bool operator==(Cancel a, Cancel b)
            {
                return (
                    a.index == b.index &&
                    a.command == b.command &&
                    a.requirement_idx == b.requirement_idx &&
                    a.extradata_idx == b.extradata_idx &&
                    a.frame_window_start == b.frame_window_start &&
                    a.frame_window_end == b.frame_window_end &&
                    a.starting_frame == b.starting_frame &&
                    a.move_id == b.move_id &&
                    a.type == b.type
                    );
            }

            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                if (!(obj is Cancel)) return false;
                return (
                    this.index == ((Cancel)obj).index &&
                    this.command == ((Cancel)obj).command &&
                    this.requirement_idx == ((Cancel)obj).requirement_idx &&
                    this.extradata_idx == ((Cancel)obj).extradata_idx &&
                    this.frame_window_start == ((Cancel)obj).frame_window_start &&
                    this.frame_window_end == ((Cancel)obj).frame_window_end &&
                    this.starting_frame == ((Cancel)obj).starting_frame &&
                    this.move_id == ((Cancel)obj).move_id &&
                    this.type == ((Cancel)obj).type
                    );
            }

            public override int GetHashCode()
            {
                return 0;
            }

            public static bool operator!=(Cancel a, Cancel b)
            {
                return (
                    a.index == b.index &&
                    a.command != b.command &&
                    a.requirement_idx != b.requirement_idx &&
                    a.extradata_idx != b.extradata_idx &&
                    a.frame_window_start != b.frame_window_start &&
                    a.frame_window_end != b.frame_window_end &&
                    a.starting_frame == b.starting_frame &&
                    a.move_id != b.move_id &&
                    a.type != b.type
                    );
            }
        }
        class File_Item
        {
            public string name;
            public ulong[] ptr;
            public File_Item()
            {
                Initialize(string.Empty);
            }
            public File_Item(string n, ulong[] o = null)
            {
                Initialize(n, o);
            }
            public void Initialize(string n, ulong[] o = null)
            {
                name = n; ptr = o;
            }
        }

        class Req_Item
        {
            public string name;
            public List<Node> ptr;
            public Req_Item()
            {
                Initialize(string.Empty);
            }
            public Req_Item(string n, List<Node> r = null)
            {
                Initialize(n, r);
            }
            public void Initialize(string n, List<Node> r = null)
            {
                name = n; ptr = r;
            }
        }

        class Node
        {
            public int index;
            public int value;
            public Node(int idx = -1, int val = -1)
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

        static MemoryHelper64 mem = null;
        public static ulong baseAddress = 0;
        // GAME VERSION: v4.20

        // Structure Addresses
        public static ulong p1struct;
        public static ulong p1profileStruct;
        public static ulong p2profileStruct;
        public static ulong visuals;
        public static ulong hud_icon_addr;
        public static ulong motbinOffset;
        public static ulong p1structsize;
        public static ulong AllocatedMem;

        // Costume Related stuff
        static readonly string cs_kaz_final = "/Game/Demo/StoryMode/Character/Sets/CS_KAZ_final.CS_KAZ_final";
        static readonly string cs_hei_final = "/Game/Demo/StoryMode/Character/Sets/CS_HEI_final.CS_HEI_final";
        static readonly string cs_mrx_final = "/Game/Demo/StoryMode/Character/Sets/CS_MRX_final.CS_MRX_final";

        static readonly List<File_Item> fileData = new List<File_Item>();
        static readonly Req_Item[] requirements = new Req_Item[5];
        static bool IsRunning = false; // Variable to check if the game is running or not
        static readonly byte[] ORG_INST = { 0x4C, 0x8B, 0x6C, 0x24, 0x68 }; // mov r13, [rsp+68]
        static byte[] BYTES_READ = { 0, 0, 0, 0, 0 }; // mov r13, [rsp+68]
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
        private void Button3_Click(object sender, EventArgs e)
        {
            panel_instructions.BringToFront();
            panel_instructions.Visible = true;
            button_back.Visible = true;
        }

        // For going back to main menu
        private void Button_Black_Click(object sender, EventArgs e)
        {
            Panels_Visibility(false);
        }

        // For Cross button
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseProgram();
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
                        c1 += 1; Thread.Sleep(500);
                    }
                    ulong[] list;
                    // Process Successfully Found
                    if (mem == null) mem = new MemoryHelper64(process);
                    else mem.SetProcess(process);
                    // Finding Addresses
                    baseAddress = mem.GetBaseAddress();
                    ClearTextBox("");
                    AppendTextBox("Attached to the game\r\nFinding Visuals Address...");
                    list = FindInList("visuals");
                    // Looping to find the address
                    while(true)
                    {
                        visuals = mem.OffsetCalculator(list);
                        if (visuals != 0) break;
                        Thread.Sleep(500);
                    }
                    AppendTextBox("Found!\r\nFinding Player 1 Profile Address...");
                    // Finding P1 Profile Structure Address
                    list = FindInList("p1profile");
                    if (list == null) // In case of an error
                    {
                        MessageBox.Show("An Error occured Reading P1 Profile Address from \"addresses.txt\". \nClosing Program.");
                        CloseProgram();
                        return;
                    }
                    while (true) // Loop to find the said address
                    {
                        Thread.Sleep(500);
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
                                c2++; Thread.Sleep(500);
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
                        Thread.Sleep(500);
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
                                c3++; Thread.Sleep(500);
                                break;  // Breaking the loop so program can loop back to attach to game
                            }
                        }
                        else break; // Address Successfully Found
                    }
                    if (c3 >= 1) continue;  // If Address not found then Loop back
                    AppendTextBox("Found!\r\n");
                    try
                    {
                        motbinOffset = FindInList("movesetOffset")[0];
                        p1struct = FindInList("p1struct")[0];
                        p1structsize = FindInList("p1structsize")[0];
                        list = FindInList("hud_icon_addr");
                        if (list != null)
                            hud_icon_addr = mem.GetBaseAddress() + list[0];
                    }
                    catch (Exception ex)
                    {
                        if ((uint)ex.HResult == 0x80004003)
                        {
                            MessageBox.Show("Addresses not found in the addresses.txt file\nClosing Program!.");
                            CloseProgram();
                        }
                        else throw ex;
                    }
                    CreateCodeCave();
                    AppendTextBox("Starting Script\r\n");
                    BossThreadLoop();
                    // Program will reach this portion ONLY if the game gets closed, so freeing memory
                    mem.VirtualFreeMemory(AllocatedMem);
                    mem.SetProcess(null);
                    process = null;
                }
                else
                {
                    ClearTextBox("");
                    AppendTextBox("Could not find TEKKEN 7. Please Run the Game\r\n");
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
            else if (charID == 26 && GetCostumeID(side) == 13) // Preset 0 or 7
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
                if (gameMode == 3 || gameMode == 15) continue;
                else if (gameMode == 4 || gameMode == 6)    // Vs mode and Player Match
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
            int charID = GetCharID(MOVESET);
            if (charID == 09)
            {
                if ((!Checkbox_Get(6)) || (GetCostumeID(side) == 4))
                {
                    Result = DVKCancelRequirements(MOVESET);
                    Checkbox_Set(1, Result);
                }
            }
            else if (charID == 08)
            {
                if ((!Checkbox_Get(6)) || (GetCostumeID(side) == 4))
                {
                    Result = ASHCancelRequirements(MOVESET);
                    Checkbox_Set(2, Result);
                }
            }
            else if (charID == 32)
            {
                if ((!Checkbox_Get(6)) || (GetCostumeID(side) == 4))
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
            //int req_idx_df2 = FindReqIdx(MOVESET, new int[] { 563, 7, 225, 1, 634, 0, 361, 1, 0x81C8, 6, 881, 0 }, 3000);
            if(!RemoveRequirements(MOVESET, FindInReqList("KAZUYA"))) return false;

            int Co_Dummy_00 = GetMoveID(MOVESET, "Co_Dummy_00\0", 563);
            int Co_Dummy_00_cancel_idx = GetMoveAttributeIndex(MOVESET, Co_Dummy_00, (int)Offsets.cancel_addr);
            if (Co_Dummy_00 < 0) return true;   // It means already written

            int Kz_vipLP = GetMoveID(MOVESET, "Kz_vipLP\0", 1400);
            if (Kz_vipLP < 0) return false;
            int Kz_majin_00 = GetMoveID(MOVESET, "Kz_majin_00\0", 1400);
            if (Kz_majin_00 < 0) return false;
            int Kz_RageArts00 = GetMoveID(MOVESET, "Kz_RageArts00\0", 2000);
            if (Kz_RageArts00 < 0) return false;

            // Writing into group cancel for Ultimate Rage Art
            Cancel RA_Cancel = new Cancel(0x4000000300000008, -1, 14, 1, 32767, 1, (short)Kz_RageArts00, 80);
            Cancel RA_Cancel2 = new Cancel(0x4000000300000008, -1, 14, 1, 1, 1, (short)Kz_RageArts00, 80);
            Cancel RA_Cancel3 = new Cancel(0x4000000300000008, -1, 14, 1, 1, 1, (short)Kz_RageArts00, 80);
            FindCancelIndex(MOVESET, ref RA_Cancel, 1, 1500);
            FindCancelIndex(MOVESET, ref RA_Cancel2, 0, 7349); // Fed old cancel index - 400 in there
            FindCancelIndex(MOVESET, ref RA_Cancel3, 0, RA_Cancel2.index+1);
            RA_Cancel.move_id = RA_Cancel2.move_id = RA_Cancel3.move_id = (short)Co_Dummy_00;
            //Debug.WriteLine("Index = " + FindCancelIndex(MOVESET, ToFind, 1, 1500).ToString());
            Cancel[] array = 
            {
                RA_Cancel
            };
            if (!Edit_Cancels(MOVESET, array, 1)) return false;

            int[] arr1 = new int[]
            {
                Kz_RageArts00, // To, From is fixed to Co_Dummy_00 (838)
            	Kz_majin_00,
                Kz_vipLP
            };
            // Copying move "RageArt00" (2103) to "Co_Dummy_00" (838)
            // Copying move "Kz_majin_00" (1658) to "Co_Dummy_02" (839)
            // Copying move "Kz_vipLP" (1600) to "Co_Dummy_03" (840)
            if (!CopyMoves(MOVESET, arr1, Co_Dummy_00)) return false;

            int ind1 = Co_Dummy_00_cancel_idx; // Cancel list index for Co_Dummy_00
            int Kz_sKAM00_ = GetMoveID(MOVESET, "Kz_sKAM00_\0", 1400);
            
            Cancel[] cancels_list =
            {
		        // For Ultimate Rage Art
		        new Cancel(ind1++, 0, 0, 11, 1, 1, 1, (short)GetMoveID(MOVESET, "SKz_RageArts01Treasure_7CS\0", 2000), 65),
                new Cancel(ind1++, 0x8000, 0, 0, 0, 0, 0, (short)Kz_sKAM00_, 336),
		        // For d/f+2,1 cancel
                new Cancel(ind1++, 0, FindIndexInList("KAZUYA",-1) + 4, 52, 23, 23, 23, (short)Kz_vipLP, 65), // 3555 + 4
                new Cancel(ind1++, 0, FindIndexInList("KAZUYA",-2), 23, 1, 32767, 1, (short)Kz_sKAM00_, 257), // 3516
                new Cancel(ind1++, 0, FindIndexInList("KAZUYA",-3), 23, 1, 32767, 1, (short)Kz_sKAM00_, 257), // 3410
                new Cancel(ind1++, 0x8000, 0, 0, 46, 32767, 46, (short)Kz_sKAM00_, 336),
		        // For f+1+2,2 cancel
		        new Cancel(ind1++, 0, FindIndexInList("KAZUYA",-4), 23, 1, 32767, 1, (short)Kz_sKAM00_, 257), // 1882
                new Cancel(ind1++, 0, FindIndexInList("KAZUYA",-5), 11, 1, 32767, 1, (short)(Kz_vipLP+1), 65), // 3191
                new Cancel(ind1++, 0, 0, 16, 32, 32, 32, (short)GetMoveID(MOVESET, "Kz_bdyTuki\0", 1400), 65),
                new Cancel(ind1++, 0x8000, 0, 0, 58, 32767, 58, (short)Kz_sKAM00_, 336),
		        // For f+1+2,2 cancel (blending)
		        new Cancel(GetMoveAttributeIndex(MOVESET, Kz_vipLP, (int)Offsets.cancel_addr) + 8, 0x4000000200000000, 0, 11, 1, 24, 24, (short)(Co_Dummy_00 + 2), 80),
		        // For d/f+1 into Ultimate RA
                RA_Cancel2,
                // For d/f+2 into Ultimate RA
                RA_Cancel3,
                // For d/f+2,1 cancel (blending)
                new Cancel(GetMoveAttributeIndex(MOVESET, Kz_majin_00, (int)Offsets.cancel_addr) + 8, 0x4000000100000000, 0, 11, 1, 13, 13, (short)(Co_Dummy_00 + 1), 80),
		        // For Stopping Story Rage Art from Coming out (cancel list: 10177, entry 2)
		        new Cancel(GetMoveAttributeIndex(MOVESET, GetMoveID(MOVESET, "SKz_RageArts_sp_nRv3\0", 2000), (int)Offsets.cancel_addr) + 1, 0, FindIndexInList("KAZUYA",-6), -1, -1, -1, -1, -1, -1) // 3624
            
            };

            // Updating cancel lists
            if (!Edit_Cancels(MOVESET, cancels_list, 0)) return false;

            // Adjusting cancel lists
            int[,] reqs = new int[,]
            {
                {Co_Dummy_00+1, Co_Dummy_00_cancel_idx + 2} // Co_Dummy_02 (839), Index number to be assigned
            };
            if (!AssignMoveAttributeIndex(MOVESET, reqs, (int)OFFSETS.cancel_list)) return false;

            // Checking if Hit Conditions needs changes or not
            ulong addr = GetMoveAttributeAddress(MOVESET, Kz_vipLP, (int)Offsets.hit_cond_addr);
            addr = mem.ReadMemory<ulong>(addr); // This will get the requirement address
            if (mem.ReadMemory<int>(addr) != 563)
            {
                return true;
            }

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
            if (!RemoveRequirements(MOVESET, FindInReqList("HEIHACHI"))) return false;

            int Co_Dummy_00 = GetMoveID(MOVESET, "Co_Dummy_00\0", 800);
            int Co_Dummy_00_cancel_idx = GetMoveAttributeIndex(MOVESET, Co_Dummy_00, (int)Offsets.cancel_addr);
            if (Co_Dummy_00 < 0) return true; // Already written
            int He_RageArts00 = GetMoveID(MOVESET, "He_RageArts00\0", 1600);
            int He_WK00F_7CS = GetMoveID(MOVESET, "He_WK00F_7CS\0", 1600);
            int He_sKAM00_ = GetMoveID(MOVESET, "He_sKAM00_\0", 1400);
            Cancel RAI_Cancel = new Cancel(0x4000000C00000020, 0, 14, 1, 32767, 1, (short)GetMoveID(MOVESET, "He_lk00\0", 1400), 80);
            Cancel RA_Cancel = new Cancel(0x4000000300000004, -1, 14, 1, 32767, 1, (short)He_RageArts00, 80);
            Cancel RA_Cancel2 = new Cancel(0x4000000300000004, -1, 14, 1, 1, 1, (short)He_RageArts00, 80);
            Cancel RA_Cancel3 = new Cancel(0x4000000300000004, -1, 14, 1, 1, 1, (short)He_RageArts00, 80);
            FindCancelIndex(MOVESET, ref RAI_Cancel, 1, 750);
            RAI_Cancel.move_id = (short)He_WK00F_7CS;
            FindCancelIndex(MOVESET, ref RA_Cancel, 1, 1400);
            FindCancelIndex(MOVESET, ref RA_Cancel2, 0, 7775);
            FindCancelIndex(MOVESET, ref RA_Cancel3, 0, RA_Cancel2.index+1);
            RA_Cancel.move_id = RA_Cancel2.move_id = RA_Cancel3.move_id = (short)Co_Dummy_00;
            // Writing into group cancels
            Cancel[] cancels =
            {
                RAI_Cancel, RA_Cancel
            };

            //long[,] arr = new long[,]
            //{
            //    {899, -1, -1, -1, -1, -1, -1, He_WK00F_7CS, -1}, // 892 + 7
            //    {1673, -1, -1, -1, -1, -1, -1, Co_Dummy_00, -1}  // 1541 + 132
            //};
            if (!Edit_Cancels(MOVESET, cancels, 1)) return false;

            // This array is for copying moves
            int[] arr1 = 
            {
                He_RageArts00 // He_RageArt00
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
            Cancel[] arr = 
            {
                // For Ultimate Rage Art
		        new Cancel(ind1++, 0, 0, 11, 7, 7, 7, (short)GetMoveID(MOVESET,"He_RageArts01_Treasure_7CS\0", 1600), 65),
                new Cancel(ind1++, 0x8000, 0, 0, 0, 0, 0, (short)He_sKAM00_, 336),
		        // For Spinning Demon (kick 1)
		        new Cancel(ind1++, 0x4000000100000000, 0, 11, 1, 15, 15, (short)GetMoveID(MOVESET,"He_m_k00AG\0", 1600), 80),
                new Cancel(ind1++, 0x400000080000004E, 0, 16, 1, 16, 16, (short)He_m_k01M_CS, 80),
                new Cancel(ind1++, 0x4000000800000000, 0, 11, 1, 15, 15, (short)GetMoveID(MOVESET,"He_m_k00DG\0", 1600), 80),
                new Cancel(ind1++, 0x8000, 0, 0, 49, 32767, 49, (short)He_sKAM00_, 336),
		        // For Spinning Demon (kick 2)
		        new Cancel(ind1++, 0x400000080000004E, 0, 16, 1, 24, 24, (short)He_m_k02M_CS, 80),
                new Cancel(ind1++, 0x4000000100000000, 0, 11, 1, 16, 16, (short)GetMoveID(MOVESET,"He_m_k01MAG\0", 1600), 80),
                new Cancel(ind1++, 0x4000000800000020, 0, 11, 1, 23, 23, (short)GetMoveID(MOVESET,"He_m_k01MDG\0", 1600), 80),
                new Cancel(ind1++, 0x8000, 0, 0, 59, 32767, 59, (short)He_sKAM00_, 336),
		        // D+1 to Ultimate RA
                RA_Cancel2,
                // D+2 to Ultimate RA
                RA_Cancel3,
                // For Spinning Demon (kick 3)
		        new Cancel(ind2 + 0, 0x4000000100000000, 0, -1, 1, -1, -1, -1, 80),
                new Cancel(ind2 + 1, 0x4000000800000020, 0, -1, 1, -1, -1, -1, 80),
                new Cancel(ind2 + 2, 0x400000080000004e, 0, -1, 1, -1, -1, -1, 80),
		        // For Spinning Demon (kick 4)
		        new Cancel(ind2 + 4, 0x4000000100000000, 0, -1, 1, -1, -1, -1, 80),
                new Cancel(ind2 + 5, 0x4000000800000020, 0, -1, 1, -1, -1, -1, 80),
                new Cancel(ind2 + 6, 0x400000080000004e, 0, -1, 1, -1, -1, -1, 80),
		        // For Spinning Demon (kick 5)
		        new Cancel(ind2 + 8, 0x4000000100000000, 0, -1, 1, -1, -1, -1, 80),
                new Cancel(ind2 + 9, 0x4000000800000020, 0, -1, 1, -1, -1, -1, 80),
                new Cancel(ind2 +10, 0x400000080000004e, 0, -1, 1, -1, -1, -1, 80),
		        // For Spinning Demon (kick 6)
		        new Cancel(ind2 +12, 0x4000000100000000, 0, -1, 1, -1, -1, -1, 80),
                new Cancel(ind2 +13, 0x4000000800000020, 0, -1, 1, -1, -1, -1, 80),
		        // From Regular Spinning Demon to boss version
		        new Cancel(ind2 +15, 0, 0, -1, -1, -1, -1, -1, -1)
            };
            if (!Edit_Cancels(MOVESET, arr, 0)) return false;

            int[,] reqs = new int[,]
            {
                {GetMoveID(MOVESET,"He_m_k00_CS\0", 1600), Co_Dummy_00_cancel_idx + 2}, // For Spinning Demon Kick 1, 4244
		        {He_m_k01M_CS, Co_Dummy_00_cancel_idx + 6}  // For Spinning Demon Kick 2, 4248
            };
            if (!AssignMoveAttributeIndex(MOVESET, reqs, (int)OFFSETS.cancel_list)) return false;

            if (!HeihachiAura(MOVESET, GetMoveAttributeAddress(MOVESET, (short)He_sKAM00_+1, (int)Offsets.ext_prop_addr))) return false;

            return true; // Successfully Written
        }

        private bool SHACancelRequirements(ulong MOVESET) // For Shin Akuma
        {
            // For removing requirements from cancels
            if (!RemoveRequirements(MOVESET, FindInReqList("AKUMA"))) return false;

            // For extra move properties
            // {MoveID, Extraprop index value to be assigned to it}
            int Mx_asyura = GetMoveID(MOVESET, "Mx_asyura\0", 1900);
            int Mx_asyura2 = GetMoveID(MOVESET, "Mx_asyura2\0", 1900);
            int Mx_asyurab = GetMoveID(MOVESET, "Mx_asyurab\0", 1900);
            int[,] arr = new int[,]
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

            int Co_t_slk00 = GetMoveID(MOVESET, "Co_t_slk00\0", 1400);
            int Mx_EXrecover_7CS = GetMoveID(MOVESET, "Mx_EXrecover_7CS\0", 1900);
            if (Mx_EXrecover_7CS < 0 || Co_t_slk00 < 0) return false;

            Cancel d34_Cancel1 = new Cancel(0x4000000C00000004, 0, 13, 1, 32767, 1, (short)Co_t_slk00, 80);
            Cancel d34_Cancel2 = new Cancel(0x4000000C00000004, 0, 13, 1, 5, 1, (short)Co_t_slk00, 80);
            FindCancelIndex(MOVESET, ref d34_Cancel1, 1, 490);
            FindCancelIndex(MOVESET, ref d34_Cancel2, 1, 690);
            d34_Cancel1.move_id = d34_Cancel2.move_id = (short)Mx_EXrecover_7CS;

            // Writing into group cancels
            Cancel[] cancels = 
            {
                d34_Cancel1, d34_Cancel2
                //{588, -1, -1, -1, -1, -1, -1, Mx_EXrecover_7CS, -1}, // 583+5 - for d+3+4 meter charge
                //{768, -1, -1, -1, -1, -1, -1, Mx_EXrecover_7CS, -1}, // 763+5 - for d+3+4 meter charge
            };
            if (!Edit_Cancels(MOVESET, cancels, 1)) return false;

            return true;  // This means the moveset has been modified successfully
        }

        private bool JINCancelRequirements(ulong MOVESET)    // Asura Jin requirements
        {
            // Editing requirements
            if (!RemoveRequirements(MOVESET, FindInReqList("JIN"))) return false;

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
		        // For d/f+3,3 / 1,3 cancel list
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
            int[,] arr = new int[,]
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
            if (!AssignMoveAttributeIndex(MOVESET, arr, (int)OFFSETS.cancel_list)) return false;

            return true; // Memory has been successfully modified
        }

        private bool BS7CancelRequirements(ulong MOVESET) // For Devil Kazumi
        {
            // For removing requirements from cancels
            // {RequirementIndex, how many requirements to zero}
            List<Node> arr = FindInReqList("KAZUMI");
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
            //requirements.Clear();
            byte[] Org = { 0x4C, 0x8B, 0x6C, 0x24, 0x68 }; // mov r13, [rsp+68]
            if (mem != null)
            {
                mem.WriteBytes(hud_icon_addr, Org);
                mem.VirtualFreeMemory(AllocatedMem);
            }
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
        ulong GetMoveAttributeAddress(ulong moveset, int moveID, int offset)
        {
            if (moveID < 0) return 0;
            ulong moves_addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.moves);
            if (moves_addr == 0) return 0;
            ulong moves_size = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.moves + 0x8);
            if (moves_size == 0) return 0;
            if ((ulong)moveID >= moves_size) return 0;
            ulong addr = moves_addr + (ulong)(moveID * 0xB0);
            ulong attr_addr = mem.ReadMemory<ulong>(addr + (ulong)offset);
            return attr_addr;
        }
        bool RemoveRequirements(ulong moveset, List<Node> arr)
        {
            if (arr == null) return true;
            ulong requirements_addr = mem.ReadMemory<ulong>(moveset + 0x160);
            if (requirements_addr == 0) return false; // Return in case of null
            ulong addr, n_addr;
            int rows = arr.Count;
            // Removing requirements from the given array
            for (int i = 0; i < rows; i++)
            {
                addr = requirements_addr + (8 * (ulong)arr[i].index);
                // Writing and replacing the code to make the HUD comeback and stop AI from reverting Devil Transformation
                if (arr[i].value == 0 && GetCharID(moveset) == 9)
                {
                    if (!mem.WriteMemory<int>(addr, 563)) return false;
                    if (!mem.WriteMemory<int>(addr + 16, 0x829D)) return false;
                    if (!mem.WriteMemory<int>(addr + 20, 1)) return false;
                }
                // Handling the requirements to allow Akuma's parry
                else if (arr[i].value == 0 && GetCharID(moveset) == 32)
                {
                    if (!mem.WriteMemory<int>(addr + 32, 0)) return false;
                    if (!mem.WriteMemory<int>(addr + 36, 0)) return false;
                    if (!mem.WriteMemory<int>(addr + 64, 0)) return false;
                    if (!mem.WriteMemory<int>(addr + 68, 0)) return false;
                    arr[i].value = 3;
                }
                for (int j = 0; j < arr[i].value; j++)
                {
                    n_addr = addr + (ulong)(8 * j);
                    if (!mem.WriteMemory<int>(n_addr, 0)) return false;
                }
            }
            return true;
        }

        int FindReqIdx(ulong moveset, int[] arr, int start = 0)
        {
            if (arr == null) return -1;
            if (arr.GetLength(0) % 2 != 0) return -1;
            ulong requirements_addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.requirements);
            if (requirements_addr == 0) return -1;
            int rows = mem.ReadMemory<int>(moveset + (int)OFFSETS.requirements + 8);
            if (rows == 0) return -1;
            if (start < 0 || start > rows) start = 0;
            int pat_size = arr.GetLength(0);
            ulong addr;
            int ind = -1;
            int value;
            rows = rows * 2 - pat_size;
            for (int i = 0; i <= rows; i++)
            {
                int j;
                // For current index i, check for pattern match
                for (j = 0; j < pat_size; j++)
                {
                    // if (txt[i + j] != pat[j]) break
                    addr = requirements_addr + (ulong)((i + j) * 4);
                    value = mem.ReadMemory<int>(addr);
                    if (value != arr[j]) break;
                }
                if (j == pat_size) // if pattern[0...M-1] = text[i, i+1, ...i+M-1]
                    ind = i;
            }
            ind = ind < 0 ? -1 : ind / 2;
            //Debug.WriteLine("Index = " + ind.ToString());
            return ind;
        }

        // Gets Move Name, based on given ID
        string GetMoveName(ulong moveset, int ID)
        {
            ulong moves_addr = mem.ReadMemory<ulong>(moveset + 0x210);
            if (moves_addr == 0) return string.Empty;
            int size = mem.ReadMemory<int>(moveset + 0x218);
            if (ID < 0 || ID >= size) return string.Empty;
            ulong move_addr = moves_addr + (ulong)(0xB0 * ID);
            ulong name_addr = mem.ReadMemory<ulong>(move_addr);
            string name = mem.ReadMemoryString(name_addr, 30);
            return name;
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
                //Debug.WriteLine(string.Format("{0:X}", ToMove));
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
        bool Edit_Cancels(ulong moveset, Cancel[] arr, int FLAG)
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
                addr = cancel_addr + (ulong)(arr[i].index * 40); // 1 cancel is of 40 bytes
                // Command
                if (arr[i].command != 0xFFFFFFFFFFFFFFFF)
                    if (!mem.WriteMemory<ulong>(addr, arr[i].command)) return false;
                // Requirement address
                if (arr[i].requirement_idx != -1)
                {
                    req = requirement + (8 * (ulong)arr[i].requirement_idx); // 1 requirement field is of 8 bytes
                    if (!mem.WriteMemory<ulong>(addr + 8, req)) return false;
                }
                // Extradata address
                if (arr[i].extradata_idx != -1)
                {
                    ext = extradata + (4 * (ulong)arr[i].extradata_idx); // 1 Extradata field is of 4 bytes
                    if (!mem.WriteMemory<ulong>(addr + 16, ext)) return false;
                }
                // Frame window start, end & starting frame
                if (arr[i].frame_window_start != -1) if (!mem.WriteMemory<int>(addr + 24, arr[i].frame_window_start)) return false;
                if (arr[i].frame_window_end != -1) if (!mem.WriteMemory<int>(addr + 28, arr[i].frame_window_end)) return false;
                if (arr[i].starting_frame != -1) if (!mem.WriteMemory<int>(addr + 32, arr[i].starting_frame)) return false;

                // Cancel move
                if (arr[i].move_id != -1) if (!mem.WriteMemory<short>(addr + 36, arr[i].move_id)) return false;
                // Cancel option
                if (arr[i].type != -1) if (!mem.WriteMemory<short>(addr + 38, arr[i].type)) return false;
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
                //idx = attribute + (ulong)(Sizes[offset] + arr[i, 1]);
                //off = (ulong)offsets[offset];

                // Getting address of the attribute
                if (offset == (int)OFFSETS.cancel_list)
                {
                    idx = attribute + (ulong)(40 * arr[i, 1]);
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

        bool HeihachiAura(ulong moveset, ulong addr)
        {
            if (addr == 0) return false;
            if (!mem.WriteMemory<int>(addr - 12, 1)) return false;
            if (!mem.WriteMemory<int>(addr - 8, 0x829d)) return false;
            if (!mem.WriteMemory<int>(addr - 4, 1)) return false;
            return true;
        }

        // Function to find the index of a certain cancel. FLAG: 0 = Normal 1 = Group Cancels
        int FindCancelIndex(ulong moveset, ref Cancel ToFind, int FLAG = 0, int start = 0)
        {
            if (FLAG < 0 || FLAG > 1) FLAG = 0;
            ulong cancels_addr;
            ulong requirement = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.requirements);
            if (requirement == 0) return -1;
            ulong extradata = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.cancel_extra);
            if (extradata == 0) return -1;
            int size;
            cancels_addr = mem.ReadMemory<ulong>(moveset + (ulong)OFFSETS.cancel_list + (ulong)(FLAG * 0x10));
            size = mem.ReadMemory<int>(moveset + (ulong)OFFSETS.cancel_list + 8 + (ulong)(FLAG * 0x10));
            if (start < 0 || start >= size) start = 0;
            Cancel Current = new Cancel();
            ToFind.index = -1;
            ulong addr;
            for (int i = start; i < size; i++)
            {
                // Calculating Address
                addr = cancels_addr + (ulong)(i * 40);
                // Reading and storing attributes of a cancel
                Current.command = mem.ReadMemory<ulong>(addr + 0x00);
                Current.requirement_idx = GetAttributeIndex(mem.ReadMemory<ulong>(addr + 0x08), requirement, 8);
                Current.extradata_idx = GetAttributeIndex(mem.ReadMemory<ulong>(addr + 0x10), extradata, 4);
                Current.frame_window_start = mem.ReadMemory<int>(addr + 0x18);
                Current.frame_window_end = mem.ReadMemory<int>(addr + 0x1C);
                Current.starting_frame = mem.ReadMemory<int>(addr + 0x20);
                Current.move_id = mem.ReadMemory<short>(addr + 0x24);
                Current.type = mem.ReadMemory<short>(addr + 0x26);
                // Making some options -1
                Current.command = (ToFind.command == 0xFFFFFFFFFFFFFFFF) ? 0xFFFFFFFFFFFFFFFF : Current.command;
                Current.requirement_idx = (ToFind.requirement_idx == -1) ? -1 : Current.requirement_idx;
                Current.extradata_idx = (ToFind.extradata_idx == -1) ? -1 : Current.extradata_idx;
                Current.frame_window_start = (ToFind.frame_window_start == -1) ? -1 : Current.frame_window_start;
                Current.frame_window_end = (ToFind.frame_window_end == -1) ? -1 : Current.frame_window_end;
                Current.starting_frame = (ToFind.starting_frame == -1) ? -1 : Current.starting_frame;
                Current.move_id = (ToFind.move_id == -1) ? (short)(-1) : Current.move_id;
                Current.type = (ToFind.type == -1) ? (short)(-1) : Current.type;
                // Comparing this with stored cancel
                if (Current == ToFind)
                {
                    ToFind.index = i;
                    return i;
                }
            }
            return -1;
        }
        
        // Calculates index of an attribute if base address and size of that attribute is given
        private int GetAttributeIndex(ulong addr, ulong base_address, int size)
        {
            if (addr == 0 || base_address == 0 || size == 0) return -1;
            return (int)((addr - base_address) / (ulong)size);
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
        bool Parse(string input)
        {
            if (input[0] == '#') return true;
            string name;
            ulong[] offsetsList = null;
            try
            {
                // Removing spaces
                input = input.Trim(); // From beginning and end
                input = String.Concat(input.Where(c => !Char.IsWhiteSpace(c))); // From middle

                // Seperating Name and Visuals
                name = input.Substring(0, input.IndexOf('='));
                input = input.Substring(input.IndexOf("=") + 1);

                // Parsing strings based on commas
                string[] offsets = input.Split(',');
                if (offsets[0] == "")
                {
                    MessageBox.Show($"Address for {name} Not Present!\n");
                    return false;
                }

                // Processing these arguements
                int len = offsets.Length;
                offsetsList = new ulong[len];
                for (int i = 0; i < len; i++)
                {
                    offsets[i] = offsets[i].Substring(offsets[i].IndexOf('x') + 1);
                    offsetsList[i] = UInt64.Parse(offsets[i], System.Globalization.NumberStyles.HexNumber);
                }
            }
            catch (Exception ex)
            {
                if ((uint)ex.HResult == 0x80131502)
                {
                    Debug.WriteLine("IndexOf() function returned -1");
                    return false;
                }
                else throw ex;
            }
            fileData.Add(new File_Item(name, offsetsList));
            return true;
        }

        bool Parse(string input, ref List<Node> list)
        {
            if (input[0] == '#') return true;
            if (input == "") return true;
            int index, value;
            int idx = input.IndexOf('#');
            if (idx != -1) input = input.Substring(0, idx);
            try
            {
                // Removing spaces
                input = input.Trim(); // From beginning and end
                input = String.Concat(input.Where(c => !Char.IsWhiteSpace(c))); // From middle

                // Seperating and Parsing Index and Count
                idx = input.IndexOf(',');
                if (idx == -1) return false;
                index = Int32.Parse(input.Substring(0, idx));
                value = Int32.Parse(input.Substring(idx + 1));

                //Console.WriteLine($"{index}, {value}");
                list.Add(new Node(index, value));
            }
            catch (Exception ex)
            {
                if ((uint)ex.HResult == 0x80131502)
                {
                    Console.WriteLine("IndexOf() function returned -1");
                    Console.ReadKey();
                    return false;
                }
                else throw ex;
            }
            //Console.WriteLine(input);
            return true;
        }

        void ReadAddressesFromFile()
        {
            string[] text;
            try
            {
                text = File.ReadAllLines("addresses.txt");
            }
            catch (Exception ex)
            {
                if ((uint)ex.HResult == 0x80070002)
                {
                    return;
                }
                else throw ex;
            }
            if (text.Length == 0)
            {
                return;
            }
            foreach (string t in text)
            {
                if (!Parse(t))
                {
                    MessageBox.Show("Invalid Data written in the file: addresses.txt\nClosing Program.");
                    CloseProgram();
                    return;
                }
            }

            string[] Names = { "JIN", "HEIHACHI", "KAZUYA", "KAZUMI", "AKUMA" };
            string path = "Requirements/";
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    requirements[i] = new Req_Item(Names[i]);
                    text = File.ReadAllLines(path + Names[i] + ".txt");
                    if (text.Length == 0)
                    {
                        return;
                    }
                    List<Node> list = new List<Node>();
                    foreach (string t in text)
                    {
                        Parse(t, ref list);
                    }
                    requirements[i].ptr = list;
                }
            }
            catch (Exception ex)
            {
                int result = ex.HResult;
                if ((uint)result == 0x80070002 || (uint)result == 0x80070003)
                {
                    return;
                }
                else throw ex;
            }
        }

        private ulong[] FindInList(string name)
        {
            foreach (File_Item a in fileData)
            {
                if (a.name == name) return a.ptr;
            }
            return null;
        }

        private List<Node> FindInReqList(string name)
        {
            foreach (Req_Item a in requirements)
            {
                if (a.name == name) return a.ptr;
            }
            return null;
        }

        private int FindIndexInList(string name, int v)
        {
            if (v >= 0) return -1;
            foreach (Req_Item a in requirements)
            {
                if (a.name == name)
                {
                    int size = a.ptr.Count;
                    for(int i = 0; i < size; i++)
                        if (a.ptr[i].value == v)
                            return a.ptr[i].index;
                }
            }
            return -1;
        }

        private bool CreateCodeCave()
        {
            if (hud_icon_addr == 0)
            {
                AppendTextBox("Code Cave Address Not Present\r\n");
                return false;
            }
            // Checking if given instruction address is the right one.
            BYTES_READ = mem.ReadMemoryBytes(hud_icon_addr, 5);
            for (int i = 0; i < 5; i++)
                if (BYTES_READ[i] != ORG_INST[i])
                {
                    AppendTextBox("Provided address for code cave is wrong, no code cave created\r\n");
                    return false;
                }
            int cave_size = 1024;
            AllocatedMem = mem.VirtualAllocate(cave_size);
            if (AllocatedMem == 0)
            {
                AppendTextBox("Failed to create code cave to load HUD icons\r\n");
                return false;
            }
            /////////////////////////////////////////////////////
            // CREATING JMP INSTRUCTION FOR OUR CREATED CODE CAVE
            const int Jmp_size = 5;
            int Jmp_val = (int)(AllocatedMem - hud_icon_addr - 5);
            byte[] jmp_addr = BitConverter.GetBytes(Jmp_val);
            byte[] jmp_inst = new byte[Jmp_size]
            {
                0xE9, jmp_addr[0], jmp_addr[1], jmp_addr[2], jmp_addr[3]
            };

            // WRITING JUMP INSTRUCTION IN THE GAME'S MEMORY
            if (!mem.WriteBytes(hud_icon_addr, jmp_inst))
            {
                AppendTextBox("Failed to create code cave to load HUD icons\r\n");
                mem.VirtualFreeMemory(AllocatedMem);
                return false;
            }

            // FILLING IN THE CODE CAVE FOR ALLOCATED REGION
            int code_size = 0;
            byte[] cave = HUD_ICON_CODE_CAVE(ref code_size);
            if (cave == null)
            {
                AppendTextBox("Failed to create code cave to load HUD icons\r\n");
                mem.VirtualFreeMemory(AllocatedMem);
                return false;
            }

            /////////////////////////////////////////////////////
            // CREATING RETURNING JUMP
            /////////////////////////////////////////////////////
            // Return address calculations:
            // index = cave_size - 3 - 1
            // addr = allocated_memory_addr + index
            // return_addr = original - addr + 5
            int index = code_size - 3 - 1;
            int return_offset = (int)(hud_icon_addr - (AllocatedMem + Convert.ToUInt64(index)) + 1); // Here comes +1
            byte[] jmp_back = BitConverter.GetBytes(return_offset);
            for (int i = 0; i < 4; i++) cave[index + i] = jmp_back[i];
            /////////////////////////////////////////////////////
            // WRITING CODE CAVE INTO MEMORY
            if (!mem.WriteBytes(AllocatedMem, cave))
            {
                AppendTextBox("Failed to create code cave to load HUD icons\r\n");
                mem.VirtualFreeMemory(AllocatedMem);
                return false;
            }
            AppendTextBox("Created Code Cave to load HUD Icons\r\n");
            return true;
        }

        private byte[] HUD_ICON_CODE_CAVE(ref int code_size)
        {
            byte[] D = BitConverter.GetBytes(AllocatedMem + 0x157);
            const int size = 619; // 619
            code_size = size;
            byte[] ptr = new byte[size]
            {
		        // newmem:
		        0x49, 0x83, 0xfc, 0x4c, 									// cmp r12,'L'
		        0x0f, 0x84, 0x07, 0x00, 0x00, 0x00, 						// je begin
		        0x48, 0x81, 0xc6, 0x10, 0x06, 0x00, 0x00, 					// add rsi,0x610
		        // begin:
		        0x66, 0x83, 0x3c, 0x24, 0x08, 								// cmp word ptr[rsp],8
		        0x0f, 0x84, 0x10, 0x00, 0x00, 0x00, 						// je heihachi
		        0x66, 0x83, 0x3c, 0x24, 0x09, 								// cmp word ptr[rsp],9
		        0x0f, 0x84, 0x44, 0x00, 0x00, 0x00, 						// je kazuya
		        0xe9, 0x35, 0x02, 0x00, 0x00, 								// jmp code
		        // heihachi:
		        0x41, 0x57, 												// push r15
		        0x41, 0x56, 												// push r14
		        0x41, 0x55, 												// push r13
		        0x83, 0xbe, 0xbc, 0x00, 0x00, 0x00, 0x02,					// cmp [rsi+BC],2
		        0x0f, 0x84, 0xa9, 0x00, 0x00, 0x00, 						// je regular_gi
		        0x83, 0xbe, 0xbc, 0x00, 0x00, 0x00, 0x04, 					// cmp [rsi+BC],4
		        0x0f, 0x84, 0x8d, 0x00, 0x00, 0x00, 						// je final_form
		        0x83, 0xbe, 0xbc, 0x00, 0x00, 0x00, 0x0a, 					// cmp [rsi+BC],10
		        0x0f, 0x84, 0x71, 0x00, 0x00, 0x00, 						// je mafia_suit
		        0x83, 0xbe, 0xbc, 0x00, 0x00, 0x00, 0x0e, 					// cmp [rsi+BC],14
		        0x0f, 0x84, 0x73, 0x00, 0x00, 0x00, 						// je final_form
		        0xe9, 0xe1, 0x00, 0x00, 0x00, 								// jmp label1
		        // kazuya:
		        0x41, 0x57, 												// push r15
		        0x41, 0x56, 												// push r14
		        0x41, 0x55, 												// push r13
		        0x83, 0xbe, 0xbc, 0x00, 0x00, 0x00, 0x04, 					// cmp [rsi+BC],4
		        0x0f, 0x84, 0x2e, 0x00, 0x00, 0x00, 						// je final_devil
		        0x83, 0xbe, 0xbc, 0x00, 0x00, 0x00, 0x07, 					// cmp [rsi+BC],7
		        0x0f, 0x84, 0x12, 0x00, 0x00, 0x00, 						// je gcorp
		        0x83, 0xbe, 0xbc, 0x00, 0x00, 0x00, 0x0b, 					// cmp [rsi+BC],11
		        0x0f, 0x84, 0x23, 0x00, 0x00, 0x00, 						// je tk7_dougi
		        0xe9, 0xaf, 0x00, 0x00, 0x00, 								// jmp label1
		        // gcorp:
		        0x49, 0xbf, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // mov r15,1
		        0xe9, 0x4b, 0x00, 0x00, 0x00, 								// jmp loop_prep
		        // final_devil:
		        0x49, 0xbf, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // mov r15,2
		        0xe9, 0x3c, 0x00, 0x00, 0x00, 								// jmp loop_prep
		        // tk7_dougi:
		        0x49, 0xbf, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // mov r15,3
		        0xe9, 0x2d, 0x00, 0x00, 0x00,  								// jmp loop_prep
		        // mafia_suit:
		        0x49, 0xbf, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // mov r15,4
		        0xe9, 0x1e, 0x00, 0x00, 0x00,  								// jmp loop_prep
		        // final_form:
		        0x49, 0xbf, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // mov r15,5
		        0xe9, 0x0f, 0x00, 0x00, 0x00,  								// jmp loop_prep
		        // regular_gi:
		        0x49, 0xbf, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // mov r15,6
		        0xe9, 0x00, 0x00, 0x00, 0x00,  								// jmp loop_prep
		        // loop_prep:
		        0x49, 0xbd, D[0], D[1], D[2], D[3], D[4], D[5], D[6], D[7], // mov r13,hud_icons 
		        // label2:
		        0x49, 0x83, 0xc5, 0x26, 									// add r13,0x26
		        0x49, 0xff, 0xcf, 											// dec r15
		        0x49, 0x83, 0xff, 0x00, 									// cmp r15,0
		        0x75, 0xf3, 												// jne label2 (add r13, 0x26) LOOPING BACK
		        0x49, 0xbe, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // mov r14,0
		        // loop1:
		        0x45, 0x8a, 0x7d, 0x00, 									// mov r15l,[r13]
		        0x46, 0x88, 0x7c, 0x35, 0x34, 								// mov [rbp+r14+0x34],r15l
		        0x49, 0xff, 0xc6, 											// inc r14
		        0x49, 0xff, 0xc5, 											// inc r13
		        0x49, 0x83, 0xfe, 0x26, 									// cmp r14,38
		        0x0f, 0x84, 0x02, 0x00, 0x00, 0x00,							// je label0 (2 addresses below) 
		        0xeb, 0xe5, 												// jmp loop1 (-27)
		        // label0:
		        0x44, 0x88, 0x65, 0x32, 									// mov [rbp+0x32],r12l
		        0x44, 0x88, 0x65, 0x4c, 									// mov [rbp+0x4C],r12l
		        0x49, 0x83, 0xfc, 0x4c,										// cmp r12,'L' 
		        0x0f, 0x84, 0x07, 0x00, 0x00, 0x00, 						// je label1 (+7)
		        0x48, 0x81, 0xee, 0x10, 0x06, 0x00, 0x00, 					// sub rsi,0x610
		        // label1:
		        0x41, 0x5d, 												// pop r13
		        0x41, 0x5e, 												// pop r14 
		        0x41, 0x5f, 												// pop r15 
		        0xe9, 0x0a, 0x01, 0x00, 0x00, 								// jmp code (+266)
		        // hud_icons:
		        // "KAZ_Story_1.HUD_CH_ICON_L_KAZ_Story_1"
		        0x4b, 0x41, 0x5a, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x31, 0x2e, 0x48, 0x55, 0x44, 0x5f, 0x43, 0x48, 0x5f, 0x49, 0x43, 0x4f, 0x4e, 0x5f, 0x4c, 0x5f, 0x4b, 0x41, 0x5a, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x31, 0x00, 
		        // "KAZ_Story_2.HUD_CH_ICON_L_KAZ_Story_2"
		        0x4b, 0x41, 0x5a, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x32, 0x2e, 0x48, 0x55, 0x44, 0x5f, 0x43, 0x48, 0x5f, 0x49, 0x43, 0x4f, 0x4e, 0x5f, 0x4c, 0x5f, 0x4b, 0x41, 0x5a, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x32, 0x00, 
		        // "KAZ_Story_6.HUD_CH_ICON_L_KAZ_Story_5"
		        0x4b, 0x41, 0x5a, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x35, 0x2e, 0x48, 0x55, 0x44, 0x5f, 0x43, 0x48, 0x5f, 0x49, 0x43, 0x4f, 0x4e, 0x5f, 0x4c, 0x5f, 0x4b, 0x41, 0x5a, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x35, 0x00, 
		        // "KAZ_Story_5.HUD_CH_ICON_L_KAZ_Story_6"
		        0x4b, 0x41, 0x5a, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x36, 0x2e, 0x48, 0x55, 0x44, 0x5f, 0x43, 0x48, 0x5f, 0x49, 0x43, 0x4f, 0x4e, 0x5f, 0x4c, 0x5f, 0x4b, 0x41, 0x5a, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x36, 0x00, 
		        //	"HEI_Story_1.HUD_CH_ICON_L_HEI_Story_1"
		        0x48, 0x45, 0x49, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x31, 0x2e, 0x48, 0x55, 0x44, 0x5f, 0x43, 0x48, 0x5f, 0x49, 0x43, 0x4f, 0x4e, 0x5f, 0x4c, 0x5f, 0x48, 0x45, 0x49, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x31, 0x00, 
		        //	"HEI_Story_2.HUD_CH_ICON_L_HEI_Story_2"
		        0x48, 0x45, 0x49, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x32, 0x2e, 0x48, 0x55, 0x44, 0x5f, 0x43, 0x48, 0x5f, 0x49, 0x43, 0x4f, 0x4e, 0x5f, 0x4c, 0x5f, 0x48, 0x45, 0x49, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x32, 0x00, 
		        //	"HEI_Story_3.HUD_CH_ICON_L_HEI_Story_3"
		        0x48, 0x45, 0x49, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x33, 0x2e, 0x48, 0x55, 0x44, 0x5f, 0x43, 0x48, 0x5f, 0x49, 0x43, 0x4f, 0x4e, 0x5f, 0x4c, 0x5f, 0x48, 0x45, 0x49, 0x5f, 0x53, 0x74, 0x6f, 0x72, 0x79, 0x5f, 0x33, 0x00, 
		        // code:
		        0x4c, 0x8b, 0x6c, 0x24, 0x68, 								// mov r13,[rsp+68]
		        0xe9, 0xea, 0xfd, 0x4c, 0x00                                // jmp [return_address]
            };
            return ptr;
        }
    }
}