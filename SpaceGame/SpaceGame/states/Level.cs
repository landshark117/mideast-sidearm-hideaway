﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using SpaceGame.graphics;
using SpaceGame.graphics.hud;
using SpaceGame.utility;
using SpaceGame.units;
using SpaceGame.equipment;

namespace SpaceGame.states
{
    class Level : Gamestate
    {
        #region classes
        public struct LevelData
        {
            public Wave.WaveData[] TrickleWaveData;
            public Wave.WaveData[] BurstWaveData;
            public UnicornData[] Unicorns;
            public FoodCart[] FoodCarts;
            public BlackHole BlackHole;
            public Vector2 PlayerStartLocation;
            public int Width, Height;
        }
        #endregion

        #region fields
        Spaceman _player;
        BlackHole _blackHole;
        Weapon _primaryWeapon, _secondaryWeapon;
        Gadget _primaryGadget, _secondaryGadget;
        Wave[] _waves;
        Unicorn[] _unicorns;
        FoodCart[] _foodCarts;
        Rectangle _levelBounds;

        GUI userInterface;
        Rectangle _cameraLock;
        Camera2D _camera;


        #endregion

        #region constructor
        public Level (int levelNumber, InventoryManager im)
            : base(false)
        {
            LevelData data = DataLoader.LoadLevel(levelNumber);
            _levelBounds = new Rectangle(0, 0, data.Width, data.Height);
            _player = new Spaceman(data.PlayerStartLocation);
            _blackHole = data.BlackHole;
            _waves = new Wave[data.TrickleWaveData.Length + data.BurstWaveData.Length];
            _camera = new Camera2D(_player.Position, _levelBounds.Width, _levelBounds.Height);
            //construct waves
            for (int i = 0; i < data.TrickleWaveData.Length; i++)
            { 
                _waves[i] = new Wave(data.TrickleWaveData[i], true, _levelBounds);
            }
            for (int i = 0; i < data.BurstWaveData.Length; i++)
            {
                _waves[i + data.TrickleWaveData.Length] = new Wave(data.BurstWaveData[i], false, _levelBounds);
            }
            //Test code to set weapons 1-6 to created weapons
            im.setPrimaryWeapon(new ProjectileWeapon("Flamethrower", _player));
            im.setSecondaryWeapon(new ProjectileWeapon("FreezeRay", _player));
            im.setPrimaryGadget(new Gadget(new Gadget.GadgetData { MaxEnergy = 1000 }));

            //Set Weapon holders in level
            _primaryWeapon = im.getPrimaryWeapon();
            _secondaryWeapon = im.getSecondaryWeapon();

            _unicorns = new Unicorn[data.Unicorns.Length];
            for (int j = 0; j < data.Unicorns.Length; j++)
            {
                _unicorns[j] = new Unicorn(data.Unicorns[j]);
            }

            _foodCarts = data.FoodCarts;

            _primaryGadget = new Gadget(new Gadget.GadgetData { MaxEnergy = 1000 });
            _primaryGadget = im.getPrimaryGadget();
            
            userInterface = new GUI(_player, _blackHole);
        }

        #endregion

        #region methods
        public override void Update(GameTime gameTime, InputManager input, InventoryManager im)
        {
            input.SetCameraOffset(_camera.Position);
            handleInput(input);
            _camera.Update(gameTime, _player.Position);
            //if player is outside static area rectangle, call update on camera to update position of camera until
            //the player is in the static area rectangle or the camera reaches the _levelbounds, in which case,
            //the camera does not move in that direction (locks)

            /*
            if ((_player.HitRect.Bottom > _cameraLock.Bottom && _player.HitRect.Top < _cameraLock.Top &&
            _player.HitRect.Right < _cameraLock.Right && _player.HitRect.Left > _cameraLock.Left) && (player is in level bounds)
            {
             * _camera.Update(gameTime);
             * _cameraLock.X = (int)(_camera.position.X + (_camera.getViewportWidth() * 0.2));
             * _cameraLock.Y = (int)(_camera.position.Y + (_camera.getViewportHeight() * 0.2));
             * 
            }*/
           
            if (_primaryGadget.Active)
                gameTime = new GameTime(gameTime.TotalGameTime, 
                    TimeSpan.FromSeconds((float)gameTime.ElapsedGameTime.TotalSeconds / 2));

            if (_blackHole.State == BlackHole.BlackHoleState.Pulling)
            {
                _blackHole.ApplyToUnit(_player, gameTime);
            }
            _player.Update(gameTime, _levelBounds);
            _primaryGadget.Update(gameTime);
            _blackHole.Update(gameTime);


            if (_blackHole.State == BlackHole.BlackHoleState.Overdrive)
            {
                foreach (Wave w in _waves)
                {
                    w.SpawnEnable = false;
                }
                foreach (Unicorn u in _unicorns)
                {
                    u.SpawnEnable = false;
                }
            }
          
            for (int i = 0; i < _waves.Length; i++)
            {
                _waves[i].Update(gameTime, _player, _blackHole, _primaryWeapon, _secondaryWeapon, _unicorns);
                //check cross-wave collisions
                if (_waves[i].Active)
                {
                    for (int j = i + 1; j < _waves.Length; j++)
                    {
                        _waves[i].CheckAndApplyCollisions(_waves[j]);
                    }
                }
            }

            for (int i = 0; i < _unicorns.Length; i++)
            {
                _unicorns[i].Update(gameTime, _levelBounds, _blackHole.Position, _player.Position, _player.HitRect);
                _unicorns[i].CheckAndApplyCollision(_player, gameTime);
                _blackHole.TryEatUnicorn(_unicorns[i], gameTime);
                for (int j = 0; j < _foodCarts.Length; j++)
                {
                    _unicorns[i].CheckAndApplyCollision(_foodCarts[j], gameTime);
                }
            }

            for (int i = 0; i < _foodCarts.Length; i++)
            {
                _foodCarts[i].Update(gameTime, _levelBounds, _blackHole.Position);
                _primaryWeapon.CheckAndApplyCollision(_foodCarts[i], gameTime.ElapsedGameTime);
                _secondaryWeapon.CheckAndApplyCollision(_foodCarts[i], gameTime.ElapsedGameTime);
                _blackHole.ApplyToUnit(_foodCarts[i], gameTime);
            }

            //Update Weapon Choice
            _primaryWeapon.Update(gameTime);
            _secondaryWeapon.Update(gameTime);
 
        }

        private void handleInput(InputManager input)
        { 
            if (input.Exit)
                this.PopState = true;

            if (_blackHole.State == BlackHole.BlackHoleState.Exhausted)
                return;

            _player.MoveDirection = input.MoveDirection;
            _player.LookDirection = XnaHelper.DirectionBetween(_player.Center, input.MouseLocation);

            if (input.FirePrimary && _player.UnitLifeState == PhysicalUnit.LifeState.Living)
            {
                _primaryWeapon.Trigger(_player.Position, input.MouseLocation);
            }
            if (input.FireSecondary && _player.UnitLifeState == PhysicalUnit.LifeState.Living)
            {
                _secondaryWeapon.Trigger(_player.Position, input.MouseLocation);
            }

            if (input.TriggerGadget1)
            {
                _primaryGadget.Trigger();
            }

            if (input.DebugKey)
            {
                _blackHole.Explode();
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, null, _camera.TransformMatrix());
            
            _blackHole.Draw(spriteBatch);
            _player.Draw(spriteBatch);
            _primaryWeapon.Draw(spriteBatch);
            _secondaryWeapon.Draw(spriteBatch);
			
            foreach (FoodCart cart in _foodCarts)
            {
                cart.Draw(spriteBatch);
            }

            foreach (Wave wave in _waves)
            {
                wave.Draw(spriteBatch);
            }
			
            foreach (Unicorn unicorn in _unicorns)
            {
                unicorn.Draw(spriteBatch);
            }
            spriteBatch.End();

            spriteBatch.Begin();
            userInterface.draw(spriteBatch);
            spriteBatch.End();

        }
        #endregion
    }
}
