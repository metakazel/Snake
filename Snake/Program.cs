﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Messaging;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace Snake
{
	// Let's play some Snake!
	static class Program
	{
		// Console width and height
		public const int Width = 80, Height = 24;

		// The number of barriers to generate for the game (excluding the walls)
		private const int NumBarriers = 3;

		// Direction of the snake, starts off moving to the right
		static SnakeDirection _direction = SnakeDirection.Right;

		// Is the game still going?
		static bool _playing = true;

		// Speed of the game (wait time in ms between frames)
		const int GameSpeed = 100;

		// The snake
		static Snake _snake = new Snake(10, 10);

		// The food object
		static Food _food;

		// List of barriers in the game
		static List<Barrier> _barriers = new List<Barrier>();

		// Random number generator
		static Random _rand = new Random();

		// Keeps track of the direction the snake is moving
		public enum SnakeDirection
		{
			Up,
			Down,
			Left,
			Right,
		};

		// Keeps track of the position of the food object
		public struct Food
		{
			public int x;
			public int y;
		};

		// container method to draw the food on the screen
		// replace this when Food becomes a separate class
		static void DrawFood(Food food)
		{
			Console.SetCursorPosition(food.x, food.y);
			Console.Write("$");
		}

		// container method to clear food from the screen
		// replace this when Food becomes a separate class
		static void ClearFood(Food food)
		{
			Console.SetCursorPosition(food.x, food.y);
			Console.Write(" ");
		}

		// Adds the four walls to the list of barriers, as well as "NumBarriers"
		// randomized barriers somewhere in the game bounds
		static void CreateRandomBarriers()
		{
			// Generate top wall
			var topWall = new Barrier();
			for (var i = 0; i < Width; i++) {
				topWall.AddSegment(i, 0);
			}
			_barriers.Add(topWall);

			// Generate bottom wall
			var bottomWall = new Barrier();
			for (var i = 0; i < Width; i++) {
				bottomWall.AddSegment(i, Height - 1);
			}
			_barriers.Add(bottomWall);

			// Generate left wall
			var leftWall = new Barrier();
			for (var i = 0; i < Height; i++) {
				leftWall.AddSegment(0, i);
			}
			_barriers.Add(leftWall);

			// Generate right wall
			var rightWall = new Barrier();
			for (var i = 0; i < Height; i++) {
				rightWall.AddSegment(Width - 1, i);
			}
			_barriers.Add(rightWall);

			// Generate "NumBarriers" random barriers
			for (var i = 0; i < NumBarriers; i++) {
				var thisBarrier = new Barrier();

				// Each barrier has a number of segments between 3 and 9
				var thisNumSegments = _rand.Next(3, 10);

				// Generate a starting point for the barrier
				var startX = _rand.Next(1, Width - 2);
				var startY = _rand.Next(1, Height - 2);

				// Add a segment at the starting point
				thisBarrier.AddSegment(startX, startY);

				// Generate "thisNumSegments" where each segment is touching
				// one of the existing segments
				for (var j = 0; j < thisNumSegments - 1; j++) {
					int randX, randY;

					// Keep generating points until we get one that doesn't conflict with
					// an existing segment
					do {
						// Get a random segment from the current barrier (starts with the starting point)
						// where coords[0] = x and coords[1] = y
						var coords = thisBarrier.GetRandomSegment(_rand);

						// Get a random number -1 to 1
						randX = _rand.Next(3) - 1;
						randY = _rand.Next(3) - 1;

						// Add that random number to each of the base coords
						randX += coords[0];
						randY += coords[1];
					} while (_barriers.Any(bar => bar.CollisionAtPoint(randX, randY)));
					

					// Add a new segment at the generated point
					thisBarrier.AddSegment(randX, randY);
				}

				// Finally add our new barrier to the list of barriers
				_barriers.Add(thisBarrier);
			}
		}

		// method to draw static objects on the screen before the game starts
		// these include barriers and walls
		static void PreRender()
		{
			foreach (var bar in _barriers) {
				bar.Render();
			}
		}

		// main game loop
		static void GameLoop()
		{
			// Initialize the food position
			_food.x = _rand.Next(Width - 2) + 1;
			_food.y = _rand.Next(Height - 2) + 1;

			// Initialize two screen buffers
			var buffers = new Buffer[2];
			for (int i = 0; i < 2; i++) {
				buffers[i] = new Buffer();
			}

			// ...and set the current buffer to the first one
			var curBuffer = 0;

			// Game logic loop
			// This runs continuously while the game is going on
			while (_playing) {
				// Keep track of time used in game logic
				var timer =  new Stopwatch();
				timer.Start();

				// Move the snake one unit in the direction it is facing
				_snake.Move(_direction);

				// If the snake runs over a food piece...
				if (_snake.DetectFood(_food)) {
					// ...add a new segment...
					_snake.AddSegment();

					// ...and move the food to a new position
					_food.x = _rand.Next(Width - 2) + 1;
					_food.y = _rand.Next(Height - 2) + 1;
				}

				// If the snake runs into a wall...
				if (_snake.DetectCollision(_barriers)) {
					// ...end the game
					_playing = false;
				} else {
					// Draw the scene to the current buffer
					try {
						buffers[curBuffer].AddIcon(_food.x, _food.y, '$');
					} catch (Exception e) {
						Console.Write(curBuffer);
					}
					_snake.Draw(buffers[curBuffer]);

					// Clear the old buffer from the screen
					buffers[(curBuffer + 1) % 2].Clear();

					// Render the current buffer to the console
					buffers[curBuffer].Render();
				}

				// Cycle the current buffer
				curBuffer = (curBuffer + 1) % 2;

				// Stop the timer and get the elapsed time in ms
				timer.Stop();
				var elapsedTime = (int)timer.Elapsed.TotalMilliseconds;

				// Sleep until the next frame
				Thread.Sleep(GameSpeed - elapsedTime);
			}
		}

		// asynchronous user input loop
		static void GetUserInput()
		{
			while (true) {
				// Read a key from the console
				var key = Console.ReadKey().Key;

				// Set the snake's direction based on which arrow key was pressed
				switch (key) {
					case ConsoleKey.UpArrow:
						_direction = SnakeDirection.Up;
						break;
					case ConsoleKey.DownArrow:
						_direction = SnakeDirection.Down;
						break;
					case ConsoleKey.LeftArrow:
						_direction = SnakeDirection.Left;
						break;
					case ConsoleKey.RightArrow:
						_direction = SnakeDirection.Right;
						break;
				}
			}
		}

		// game start function
		static void Main(string[] args)
		{
			// initialize all our threads
			var gameLoop = new Thread(new ThreadStart(GameLoop));
			var userInputLoop = new Thread(new ThreadStart(GetUserInput));
			
			// Create walls and barriers for the game
			CreateRandomBarriers();

			// draw static objects including walls and barriers
			PreRender();

			// start the threads
			gameLoop.Start();
			userInputLoop.Start();

			// wait for the game loop to end
			gameLoop.Join();

			// Stop the user input loop
			userInputLoop.Abort();

			// Print the ending message
			Console.SetCursorPosition(0, Height);
			Console.Write("\bYou are a loser!");

			// Wait for the user to quit
			Console.ReadKey();
		}
	}
}